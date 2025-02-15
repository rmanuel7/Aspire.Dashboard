using System.Security.Claims;
using System.Text.Encodings.Web;
using Aspire.Dashboard.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard.Authentication;

public sealed class OtlpCompositeAuthenticationHandler : AuthenticationHandler<OtlpCompositeAuthenticationHandlerOptions>
{
    private readonly IOptionsMonitor<DashboardOptions> _dashboardOptions;

    public OtlpCompositeAuthenticationHandler(IOptionsMonitor<DashboardOptions> dashboardOptions, IOptionsMonitor<OtlpCompositeAuthenticationHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _dashboardOptions = dashboardOptions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var options = _dashboardOptions.CurrentValue;

        foreach (var scheme in GetRelevantAuthenticationSchemes(options))
        {
            var result = await Context.AuthenticateAsync(scheme).ConfigureAwait(false);

            if (result.Failure is not null)
            {
                return result;
            }
        }

        var id = new ClaimsIdentity([new Claim(OtlpAuthorization.OtlpClaimName, bool.TrueString)]);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name));
    }

    private IEnumerable<string> GetRelevantAuthenticationSchemes(DashboardOptions options)
    {
        yield return ConnectionTypeAuthenticationDefaults.AuthenticationSchemeOtlp;

        if (options.Otlp.AuthMode is OtlpAuthMode.ApiKey)
        {
            yield return OtlpApiKeyAuthenticationDefaults.AuthenticationScheme;
        }
        else if (options.Otlp.AuthMode is OtlpAuthMode.ClientCertificate)
        {
            yield return CertificateAuthenticationDefaults.AuthenticationScheme;
        }
    }
}

