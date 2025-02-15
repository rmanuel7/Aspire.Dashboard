using Microsoft.AspNetCore.Connections;

namespace Aspire.Dashboard.Authentication;

/// <summary>
/// This connection middleware registers an OTLP feature on the connection.
/// OTLP services check for this feature when authorizing incoming requests to
/// ensure OTLP is only available on specified connections.
/// </summary>
internal sealed class ConnectionTypeMiddleware
{
    private readonly List<ConnectionType> _connectionTypes;
    private readonly ConnectionDelegate _next;

    public ConnectionTypeMiddleware(ConnectionType[] connectionTypes, ConnectionDelegate next)
    {
        _connectionTypes = connectionTypes.ToList();

        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task OnConnectionAsync(ConnectionContext context)
    {
        context.Features.Set<IConnectionTypeFeature>(
            new ConnectionTypeFeature
            {
                ConnectionTypes = _connectionTypes
            });

        await _next(context).ConfigureAwait(false);
    }
}
