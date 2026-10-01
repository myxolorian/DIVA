using Diva.Api.Auth;

namespace Diva.Api.Features.Common;

/// <summary>
/// Headers that tell the browser to be strict with DIVA's pages: no sniffing file types, no
/// showing the app inside another site's frame, and (CSP) only running scripts that come from
/// DIVA itself. If an attacker ever got text like &lt;script&gt; into the page, the browser
/// would refuse to run it.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// 'unsafe-inline' for styles only: the frontend sets a few style="" attributes. Scripts stay
    /// strict. connect-src allows the login call to Supabase Auth.
    /// </summary>
    public static string ContentSecurityPolicy(string? supabaseUrl)
    {
        var supabase = SupabaseAuthOptions.TryGetBaseUrl(supabaseUrl, out var baseUrl) ? $" {baseUrl}" : "";
        return "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
               $"font-src 'self'; connect-src 'self'{supabase}; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IConfiguration configuration)
    {
        var csp = ContentSecurityPolicy(configuration[$"{SupabaseAuthOptions.SectionName}:Url"]);
        return app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers.ContentSecurityPolicy = csp;
            return next(context);
        });
    }
}
