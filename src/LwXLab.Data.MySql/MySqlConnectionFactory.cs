using System;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace LwXLab.Data.MySql;

public sealed class MySqlConnectionFactory : IDatabaseConnectionFactory<MySqlConnection>
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(MySqlDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = MySqlConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.MySql;

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
