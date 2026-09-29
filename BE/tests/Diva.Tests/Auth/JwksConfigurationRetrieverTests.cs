using Diva.Api.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Diva.Tests.Auth;

public class JwksConfigurationRetrieverTests
{
    private sealed class FakeDocumentRetriever(string json) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel) => Task.FromResult(json);
    }

    [Fact]
    public async Task Keys_from_a_supabase_shaped_jwks_can_verify_a_token()
    {
        using var supabase = new TestJwtIssuer("supabase-key-id");
        var retriever = new JwksConfigurationRetriever(TestJwtIssuer.Issuer);

        var configuration = await retriever.GetConfigurationAsync(
            "https://unused", new FakeDocumentRetriever(supabase.CreateJwksJson()), CancellationToken.None);

        Assert.Single(configuration.SigningKeys);
        Assert.Equal("supabase-key-id", configuration.SigningKeys.Single().KeyId);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            supabase.CreateToken(),
            new TokenValidationParameters
            {
                ValidIssuer = TestJwtIssuer.Issuer,
                ValidAudience = TestJwtIssuer.Audience,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            });

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task Keys_from_a_different_jwks_cannot_verify_the_token()
    {
        using var supabase = new TestJwtIssuer("kid-1");
        using var someoneElse = new TestJwtIssuer("kid-1");
        var configuration = await new JwksConfigurationRetriever(TestJwtIssuer.Issuer).GetConfigurationAsync(
            "https://unused", new FakeDocumentRetriever(someoneElse.CreateJwksJson()), CancellationToken.None);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            supabase.CreateToken(),
            new TokenValidationParameters
            {
                ValidIssuer = TestJwtIssuer.Issuer,
                ValidAudience = TestJwtIssuer.Audience,
                IssuerSigningKeys = configuration.SigningKeys,
            });

        Assert.False(result.IsValid);
    }
}
