using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using LwXLab.Data;

namespace LwXLab.Data.Supabase;

public sealed class SupabaseConnectionFactory : IDatabaseConnectionFactory<NpgsqlConnection>
{
    private readonly string _connectionString;

    public SupabaseConnectionFactory(SupabaseDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _connectionString = SupabaseConnectionString.Create(options);
    }

    public string ProviderName => DatabaseProviderNames.Supabase;

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
