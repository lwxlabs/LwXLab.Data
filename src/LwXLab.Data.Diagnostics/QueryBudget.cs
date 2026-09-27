using System;
using System.Collections.Generic;
using System.Threading;

namespace LwXLab.Data.Diagnostics;

public sealed class QueryBudgetSnapshot
{
    internal QueryBudgetSnapshot(string name) { Name = name; }
    public string Name { get; }
    public int QueryCount { get; internal set; }
    public TimeSpan DatabaseTime { get; internal set; }
    public Dictionary<string, int> Fingerprints { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
}

public interface IQueryBudgetAccessor
{
    QueryBudgetSnapshot? Current { get; }
    IDisposable BeginScope(string name);
}

internal sealed class QueryBudgetAccessor : IQueryBudgetAccessor
{
    private static readonly AsyncLocal<QueryBudgetSnapshot?> State = new AsyncLocal<QueryBudgetSnapshot?>();
    public QueryBudgetSnapshot? Current => State.Value;

    public IDisposable BeginScope(string name)
    {
        var previous = State.Value;
        State.Value = new QueryBudgetSnapshot(string.IsNullOrWhiteSpace(name) ? "operation" : name);
        return new Scope(() => State.Value = previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;
        public Scope(Action dispose) { _dispose = dispose; }
        public void Dispose() { if (_disposed) return; _disposed = true; _dispose(); }
    }
}
