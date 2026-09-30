using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Diva.Api.Auth;

/// <summary>
/// Downloads a JWKS document (a JSON list of public keys) and turns it into the configuration
/// object that the JwtBearer middleware validates signatures with.
/// Supabase serves its keys at /auth/v1/.well-known/jwks.json rather than through the usual
/// OpenID Connect discovery document, so the built-in retriever cannot be used.
/// </summary>
public sealed class JwksConfigurationRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        var json = await retriever.GetDocumentAsync(address, cancel);
        var keySet = new JsonWebKeySet(json);

        var configuration = new OpenIdConnectConfiguration { Issuer = issuer, JsonWebKeySet = keySet };
        foreach (var key in keySet.GetSigningKeys())
        {
            configuration.SigningKeys.Add(key);
        }

        return configuration;
    }
}
