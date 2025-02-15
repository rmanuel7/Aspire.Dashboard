using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Aspire.Dashboard.Authentication;

internal static class ListenOptionsConnectionTypeExtensions
{
    public static void UseConnectionTypes(this ListenOptions listenOptions, ConnectionType[] connectionTypes)
    {
        listenOptions.Use(next => new ConnectionTypeMiddleware(connectionTypes, next).OnConnectionAsync);
    }
}
