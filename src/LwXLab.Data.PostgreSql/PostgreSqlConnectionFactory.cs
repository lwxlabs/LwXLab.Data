using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using LwXLab.Data;

namespace LwXLab.Data.PostgreSql;

public sealed class PostgreSqlConnectionFactory : IDatabaseConnectionFactory<NpgsqlConnection>
{
    private readonly string _connectionString;

    public PostgreSqlConnectionFactory(PostgreSqlDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = PostgreSqlConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.PostgreSql;

    public NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        var connection = CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
