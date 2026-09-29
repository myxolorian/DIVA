using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Diva.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public const string DevBypassSetting = "Auth:DevBypass";

    public static bool IsDevBypassEnabled(this IConfiguration configuration) =>
        configuration.GetValue<bool>(DevBypassSetting);

    /// <summary>
    /// Wires up login: tokens issued by Supabase Auth are accepted, everything else is rejected.
    /// After this, every endpoint requires a logged-in user unless it opts out with AllowAnonymous().
    /// </summary>
    public static IServiceCollection AddDivaAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (configuration.IsDevBypassEnabled())
        {
            // Fail fast: a forgotten flag in production would switch login off for everybody.
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{DevBypassSetting}=true is only allowed in the Development environment " +
                    $"(current environment: {environment.EnvironmentName}).");
            }

            services.AddAuthentication(DevBypassHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevBypassHandler>(DevBypassHandler.SchemeName, _ => { });
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

            services.AddOptions<SupabaseAuthOptions>()
                .Bind(configuration.GetSection(SupabaseAuthOptions.SectionName))
                .Validate(
                    o => SupabaseAuthOptions.TryGetBaseUrl(o.Url, out _),
                    "Supabase:Url must be an absolute https URL such as https://<project-ref>.supabase.co")
                .ValidateOnStart();

            // "Configure with dependencies": the JwtBearer settings are built from SupabaseAuthOptions,
            // which is only available once the whole configuration has been loaded.
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<SupabaseAuthOptions>>((jwt, supabase) => ConfigureJwt(jwt, supabase.Value));
        }

        services.AddAuthorization(options =>
        {
            // Secure by default: an endpoint someone forgot to protect is still protected.
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        return services;
    }

    private static void ConfigureJwt(JwtBearerOptions jwt, SupabaseAuthOptions supabase)
    {
        // Keep claim names as they appear in the token ("sub", "email") instead of Microsoft's long URIs.
        jwt.MapInboundClaims = false;

        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabase.Issuer,
            ValidateAudience = true,
            ValidAudience = supabase.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            // Allow-list of signature algorithms. Blocks forged tokens that claim "alg: none" or HS256.
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "email",
        };

        // The public keys come from Supabase's JWKS endpoint. ConfigurationManager caches them and
        // downloads them again periodically (and after an unknown key id shows up), so key rotation works.
        // RefreshInterval is also how long it waits before retrying after a failed download; the default
        // of 5 minutes would keep everybody locked out that long after a network hiccup.
        jwt.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            supabase.JwksUrl,
            new JwksConfigurationRetriever(supabase.Issuer),
            new HttpDocumentRetriever(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }) { RequireHttps = true })
        {
            AutomaticRefreshInterval = TimeSpan.FromHours(1),
            RefreshInterval = TimeSpan.FromSeconds(30),
        };
    }
}
