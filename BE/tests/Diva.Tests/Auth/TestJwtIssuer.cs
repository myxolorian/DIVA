using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Diva.Tests.Auth;

/// <summary>
/// Plays the role of Supabase Auth in tests: owns an ES256 (P-256) key pair, signs tokens with the
/// private key and can publish the public key the way Supabase does. No network or Supabase needed.
/// </summary>
public sealed class TestJwtIssuer : IDisposable
{
    public const string ProjectUrl = "https://test-project.supabase.co";
    public const string Issuer = ProjectUrl + "/auth/v1";
    public const string Audience = "authenticated";
    public const string DefaultKeyId = "test-key-1";

    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public TestJwtIssuer(string keyId = DefaultKeyId)
    {
        KeyId = keyId;
        SigningKey = new ECDsaSecurityKey(_ecdsa) { KeyId = keyId };
    }

    public string KeyId { get; }
    public SecurityKey SigningKey { get; }

    public string CreateToken(
        string? issuer = Issuer,
        string? audience = Audience,
        DateTime? expires = null,
        string subject = "0b5c0d2e-8f3a-4c1e-9d7a-123456789abc",
        string email = "owner@example.com",
        SigningCredentials? credentials = null)
    {
        var expiry = expires ?? DateTime.UtcNow.AddHours(1);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", subject),
                new Claim("email", email),
                new Claim("role", "authenticated"),
            ]),
            // nbf must not be after exp, or the handler refuses to build the token (matters for expired ones).
            NotBefore = expiry.AddHours(-2),
            Expires = expiry,
            SigningCredentials = credentials ?? new SigningCredentials(SigningKey, SecurityAlgorithms.EcdsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>The key set as JwtBearer wants it, without going through the network.</summary>
    public OpenIdConnectConfiguration CreateConfiguration()
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(SigningKey);
        return configuration;
    }

    /// <summary>The JWKS JSON in the same shape Supabase serves (no "use" field, has key_ops and ext).</summary>
    public string CreateJwksJson()
    {
        var parameters = _ecdsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    alg = "ES256",
                    crv = "P-256",
                    ext = true,
                    key_ops = new[] { "verify" },
                    kid = KeyId,
                    kty = "EC",
                    x = Base64UrlEncoder.Encode(parameters.Q.X),
                    y = Base64UrlEncoder.Encode(parameters.Q.Y),
                },
            },
        });
    }

    public void Dispose() => _ecdsa.Dispose();
}
