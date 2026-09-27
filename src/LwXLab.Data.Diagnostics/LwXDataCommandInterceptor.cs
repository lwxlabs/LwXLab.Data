using System;
using System.Data.Common;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LwXLab.Data.Diagnostics;

internal sealed class LwXDataCommandInterceptor : DbCommandInterceptor
{
    private readonly LwXDataDiagnosticsOptions _options;
    private readonly IQueryBudgetAccessor _budget;
    private readonly ILogger<LwXDataCommandInterceptor> _logger;
    private readonly ConditionalWeakTable<DbCommand, Activity> _activities = new ConditionalWeakTable<DbCommand, Activity>();

    public LwXDataCommandInterceptor(LwXDataDiagnosticsOptions options, IQueryBudgetAccessor budget, ILogger<LwXDataCommandInterceptor> logger)
    {
        _options = options; _budget = budget; _logger = logger;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Before(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Before(command); return new ValueTask<InterceptionResult<DbDataReader>>(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { Before(command); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Before(command); return new ValueTask<InterceptionResult<int>>(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    { Before(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { Before(command); return new ValueTask<InterceptionResult<object>>(result); }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { After(command, eventData.Duration); return result; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { After(command, eventData.Duration); return new ValueTask<DbDataReader>(result); }
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { After(command, eventData.Duration); return result; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { After(command, eventData.Duration); return new ValueTask<int>(result); }
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { After(command, eventData.Duration); return result; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { After(command, eventData.Duration); return new ValueTask<object?>(result); }

    public override InterceptionResult DataReaderDisposing(DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        if (_options.EnableQueryDiagnostics && eventData.ReadCount >= _options.LargeResultSetWarningRows)
            _logger.LogWarning("Large database result set: {ReadCount} read operations, fingerprint {Fingerprint}.", eventData.ReadCount, Fingerprint(command.CommandText));
        return result;
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LwXDataTelemetry.QueryFailureCounter.Add(1);
        StopActivity(command, eventData.Exception);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        LwXDataTelemetry.QueryFailureCounter.Add(1);
        StopActivity(command, eventData.Exception);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void Before(DbCommand command)
    {
        if (_options.EnableEnvironmentProtection
            && _options.BlockDestructiveCommandsInProduction
            && _options.Environment == LwXLab.Data.DatabaseEnvironment.Production
            && IsDestructive(command.CommandText))
        {
            throw new InvalidOperationException("LwXLab.Data environment protection blocked a destructive database command in Production. Disable the guard explicitly only for an intentional operation.");
        }

        StartActivity(command);

        if (_options.EnableQueryDiagnostics && _options.WarnOnPotentiallyUnboundedQueries && IsPotentiallyUnbounded(command.CommandText))
            _logger.LogWarning("Potentially unbounded SELECT detected, fingerprint {Fingerprint}. Verify that returning all rows is intentional.", Fingerprint(command.CommandText));

        var current = _budget.Current;
        if (current == null || (!_options.EnableQueryBudget && !_options.EnableQueryDiagnostics)) return;
        int queryCount;
        lock (current)
        {
            current.QueryCount++;
            queryCount = current.QueryCount;
            var fingerprint = Fingerprint(command.CommandText);
            current.Fingerprints[fingerprint] = current.Fingerprints.TryGetValue(fingerprint, out var count) ? count + 1 : 1;
        }

        if (_options.EnableQueryBudget && queryCount == _options.QueryBudgetWarningCount)
            _logger.LogWarning("Database query budget warning for {BudgetName}: {QueryCount} queries.", current.Name, queryCount);

        if (_options.EnableQueryBudget && queryCount > _options.QueryBudgetMaximumCount)
        {
            LwXDataTelemetry.BudgetExceededCounter.Add(1);
            if (_options.ThrowWhenQueryBudgetExceeded)
            {
                var exception = new InvalidOperationException("Database query budget exceeded for '" + current.Name + "'.");
                StopActivity(command, exception);
                throw exception;
            }
        }
    }

    private void After(DbCommand command, TimeSpan duration)
    {
        StopActivity(command, null);
        LwXDataTelemetry.QueryCounter.Add(1);
        LwXDataTelemetry.QueryDuration.Record(duration.TotalMilliseconds);
        var current = _budget.Current;
        if (current != null)
        {
            lock (current) current.DatabaseTime += duration;
        }

        if (!_options.EnableQueryDiagnostics) return;
        if (duration >= _options.SlowQueryThreshold)
        {
            if (_options.IncludeSqlTextInLogs)
                _logger.LogWarning("Slow database query ({DurationMs} ms): {Sql}", duration.TotalMilliseconds, command.CommandText);
            else
                _logger.LogWarning("Slow database query ({DurationMs} ms), fingerprint {Fingerprint}.", duration.TotalMilliseconds, Fingerprint(command.CommandText));
        }

        if (current != null)
        {
            var fp = Fingerprint(command.CommandText);
            int count;
            lock (current) current.Fingerprints.TryGetValue(fp, out count);
            if (count == _options.RepeatedQueryWarningCount)
                _logger.LogWarning("Possible repeated/N+1 query pattern in {BudgetName}: fingerprint {Fingerprint} executed {Count} times.", current.Name, fp, count);
        }
    }

    private void StartActivity(DbCommand command)
    {
        if (!LwXDataTelemetry.ActivitySource.HasListeners()) return;
        var activity = LwXDataTelemetry.ActivitySource.StartActivity("database.query", ActivityKind.Client);
        if (activity == null) return;
        activity.SetTag("db.operation.name", GetOperation(command.CommandText));
        activity.SetTag("db.namespace", command.Connection?.Database);
        _activities.Remove(command);
        _activities.Add(command, activity);
    }

    private void StopActivity(DbCommand command, Exception? exception)
    {
        if (!_activities.TryGetValue(command, out var activity)) return;
        _activities.Remove(command);
        if (exception != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            activity.SetTag("error.type", exception.GetType().FullName);
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
        activity.Stop();
    }

    private static string GetOperation(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return "UNKNOWN";
        var value = sql.TrimStart();
        var space = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        return (space < 0 ? value : value.Substring(0, space)).ToUpperInvariant();
    }

    private static bool IsPotentiallyUnbounded(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var value = " " + sql.Replace("\r", " ").Replace("\n", " ").ToUpperInvariant() + " ";
        if (value.IndexOf(" SELECT ", StringComparison.Ordinal) < 0) return false;
        if (value.IndexOf(" WHERE ", StringComparison.Ordinal) >= 0) return false;
        if (value.IndexOf(" LIMIT ", StringComparison.Ordinal) >= 0) return false;
        if (value.IndexOf(" TOP ", StringComparison.Ordinal) >= 0) return false;
        if (value.IndexOf(" FETCH NEXT ", StringComparison.Ordinal) >= 0) return false;
        return true;
    }

    private static bool IsDestructive(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var s = sql.ToUpperInvariant();
        return s.Contains("DROP TABLE") || s.Contains("DROP DATABASE") || s.Contains("TRUNCATE TABLE") || s.Contains("DROP COLUMN");
    }

    private static string Fingerprint(string sql)
    {
        using (var sha = SHA256.Create())
        {
            var normalized = (sql ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var sb = new StringBuilder(16);
            for (var i = 0; i < 8 && i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
