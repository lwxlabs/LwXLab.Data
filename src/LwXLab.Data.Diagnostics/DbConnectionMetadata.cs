using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LwXLab.Data.Diagnostics;

internal static class DbConnectionMetadata
{
    public static async Task<string> ReadServerVersionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        var openedHere = false;
        if (connection.State == ConnectionState.Broken)
            await connection.CloseAsync().ConfigureAwait(false);

        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            openedHere = true;
        }

        try
        {
            return connection.ServerVersion;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
