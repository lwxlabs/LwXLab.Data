using System;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace LwXLab.Data.Oracle;

public sealed class OracleConnectionFactory : IDatabaseConnectionFactory<OracleConnection>
{
    private readonly string _connectionString;

    public OracleConnectionFactory(OracleDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = OracleConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.Oracle;

    public OracleConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<OracleConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
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
