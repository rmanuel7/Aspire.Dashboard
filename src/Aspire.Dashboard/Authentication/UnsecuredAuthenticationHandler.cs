using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard.Authentication;

public class UnsecuredAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public UnsecuredAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var laimsIdentity = new ClaimsIdentity(
            claims: [
                new Claim(ClaimTypes.NameIdentifier, "Local"),
                new Claim(FrontendAuthorizationDefaults.UnsecuredClaimName, bool.TrueString)
            ],
            authenticationType: FrontendAuthenticationDefaults.AuthenticationSchemeUnsecured);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(
                principal: new ClaimsPrincipal(identity: laimsIdentity),
                authenticationScheme: Scheme.Name)));
    }
}
