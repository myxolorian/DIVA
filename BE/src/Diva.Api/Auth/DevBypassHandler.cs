using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Diva.Api.Auth;

/// <summary>
/// A fake login for local development: every request is treated as coming from one dev user.
/// It exists so the API can be tried out without a Supabase account. It is only ever registered
/// when Auth:DevBypass=true AND the environment is Development (see AddDivaAuthentication).
/// </summary>
public sealed class DevBypassHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevBypass";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "11111111-1111-1111-1111-111111111111"),
                new Claim("email", "dev@diva.local"),
                new Claim("role", "authenticated"),
            ],
            authenticationType: SchemeName,
            nameType: "email",
            roleType: "role");

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
