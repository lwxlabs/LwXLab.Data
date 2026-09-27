using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace LwXLab.Data.MariaDb;

public sealed class MariaDbConnectionFactory : IDatabaseConnectionFactory<MySqlConnection>
{
    private readonly string _connectionString;

    public MariaDbConnectionFactory(MariaDbDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = MariaDbConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.MariaDb;

    public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
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
