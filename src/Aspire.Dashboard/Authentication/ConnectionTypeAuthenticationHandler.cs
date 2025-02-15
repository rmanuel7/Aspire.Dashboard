using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard.Authentication;

public class ConnectionTypeAuthenticationHandler : AuthenticationHandler<ConnectionTypeAuthenticationHandlerOptions>
{
    public ConnectionTypeAuthenticationHandler(IOptionsMonitor<ConnectionTypeAuthenticationHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {

    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var connectionTypeFeature = Context.Features.Get<IConnectionTypeFeature>();

        if (connectionTypeFeature == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("No type specified on this connection."));
        }

        if (!connectionTypeFeature.ConnectionTypes.Contains(Options.RequiredConnectionType))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Connection type {Options.RequiredConnectionType} is not enabled on this connection."));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

