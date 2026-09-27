using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LwXLab.Data;

/// <summary>Creates provider-specific ADO.NET connections without coupling callers to configuration storage.</summary>
public interface IDatabaseConnectionFactory<TConnection>
    where TConnection : DbConnection
{
    string ProviderName { get; }

    TConnection CreateConnection();

    Task<TConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
}
