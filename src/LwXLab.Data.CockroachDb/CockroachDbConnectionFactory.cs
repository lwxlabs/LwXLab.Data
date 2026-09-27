using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace LwXLab.Data.CockroachDb;

public sealed class CockroachDbConnectionFactory : IDatabaseConnectionFactory<NpgsqlConnection>
{
    private readonly string _connectionString;

    public CockroachDbConnectionFactory(CockroachDbDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = CockroachDbConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.CockroachDb;

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
