using System.Security.Claims;

namespace Diva.Api.Features.Account;

public sealed record MeResponse(string Id, string? Email);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // No AllowAnonymous() here, so the fallback policy applies: a valid token is required.
        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new MeResponse(
            Id: user.FindFirst("sub")?.Value ?? "",
            Email: user.FindFirst("email")?.Value)));

        return app;
    }
}
