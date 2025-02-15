using System.Text.Encodings.Web;
using Aspire.Dashboard.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard.Authentication;


public sealed class FrontendCompositeAuthenticationHandler : AuthenticationHandler<FrontendCompositeAuthenticationHandlerOptions>
{
    private readonly IOptionsMonitor<DashboardOptions> _dashboardOptions;

    public FrontendCompositeAuthenticationHandler(IOptionsMonitor<DashboardOptions> dashboardOptions, IOptionsMonitor<FrontendCompositeAuthenticationHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _dashboardOptions = dashboardOptions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var result = await Context.AuthenticateAsync(ConnectionTypeAuthenticationDefaults.AuthenticationSchemeFrontend).ConfigureAwait(false);

        if (result.Failure is not null)
        {
            return AuthenticateResult.Fail(
                result.Failure,
                new AuthenticationProperties(
                    items: new Dictionary<string, string?>(),
                    parameters: new Dictionary<string, object?>
                    {
                        [key: AspirePolicyEvaluator.SuppressChallengeKey] = true
                    }));
        }

        result = await Context.AuthenticateAsync().ConfigureAwait(false);

        return result;
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var scheme = GetRelevantAuthenticationScheme();

        if (scheme != null)
        {
            await Context.ChallengeAsync(scheme).ConfigureAwait(false);
        }
    }

    private string? GetRelevantAuthenticationScheme()
    {
        return _dashboardOptions.CurrentValue.Frontend.AuthMode switch
        {
            FrontendAuthMode.OpenIdConnect => FrontendAuthenticationDefaults.AuthenticationSchemeOpenIdConnect,
            FrontendAuthMode.BrowserToken => FrontendAuthenticationDefaults.AuthenticationSchemeBrowserToken,
            _ => null
        };
    }
}
