using Diva.Api.Auth;

namespace Diva.Api.Features.AppConfig;

/// <summary>What the login page needs to talk to Supabase Auth. Nothing secret.</summary>
public sealed record AppConfigResponse(string? SupabaseUrl, string? SupabasePublishableKey, bool DevBypass);

public static class AppConfigEndpoints
{
    public const string PublishableKeySetting = "Supabase:PublishableKey";

    public static IEndpointRouteBuilder MapAppConfigEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: the frontend reads it before anyone has logged in. The publishable key is meant
        // to be public (it only identifies the project); data stays protected by login + RLS.
        app.MapGet("/api/public/config", (IConfiguration config) =>
            {
                var url = SupabaseAuthOptions.TryGetBaseUrl(config[$"{SupabaseAuthOptions.SectionName}:Url"], out var baseUrl)
                    ? baseUrl
                    : null;
                return Results.Ok(new AppConfigResponse(url, config[PublishableKeySetting], config.IsDevBypassEnabled()));
            })
            .AllowAnonymous()
            .WithTags("Config");

        return app;
    }
}
