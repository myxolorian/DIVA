namespace Diva.Api.Auth;

/// <summary>
/// Settings for checking the access tokens that Supabase Auth hands out after a login.
/// Bound from the "Supabase" section of appsettings.json (or env vars such as Supabase__Url).
/// </summary>
public sealed class SupabaseAuthOptions
{
    public const string SectionName = "Supabase";

    /// <summary>Project URL, e.g. https://xxxx.supabase.co. A trailing /rest/v1/ is tolerated.</summary>
    public string Url { get; set; } = "";

    /// <summary>Supabase puts "authenticated" in the aud claim of every logged-in user's token.</summary>
    public string Audience { get; set; } = "authenticated";

    /// <summary>Scheme + host only, so pasting https://xxxx.supabase.co/rest/v1/ still works.</summary>
    public string BaseUrl => TryGetBaseUrl(Url, out var baseUrl)
        ? baseUrl
        : throw new InvalidOperationException($"Supabase:Url is not a valid https URL: '{Url}'.");

    /// <summary>Value of the iss claim in every token issued by this project.</summary>
    public string Issuer => $"{BaseUrl}/auth/v1";

    /// <summary>Where Supabase publishes the public keys that verify its tokens.</summary>
    public string JwksUrl => $"{Issuer}/.well-known/jwks.json";

    public static bool TryGetBaseUrl(string? url, out string baseUrl)
    {
        baseUrl = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        baseUrl = $"{uri.Scheme}://{uri.Authority}";
        return true;
    }
}
