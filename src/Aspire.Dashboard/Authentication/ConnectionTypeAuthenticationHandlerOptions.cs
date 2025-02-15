using Microsoft.AspNetCore.Authentication;

namespace Aspire.Dashboard.Authentication;

public sealed class ConnectionTypeAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
    public ConnectionType RequiredConnectionType { get; set; }
}
