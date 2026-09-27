using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using LwXLab.Data;

namespace LwXLab.Data.SqlServer;

public sealed class SqlServerConnectionFactory : IDatabaseConnectionFactory<SqlConnection>
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(SqlServerDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = SqlServerConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.SqlServer;

    public SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
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
