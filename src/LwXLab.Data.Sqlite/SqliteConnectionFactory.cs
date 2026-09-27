using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace LwXLab.Data.Sqlite;

public sealed class SqliteConnectionFactory : IDatabaseConnectionFactory<SqliteConnection>
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(SqliteDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = SqliteConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.Sqlite;

    public SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
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
