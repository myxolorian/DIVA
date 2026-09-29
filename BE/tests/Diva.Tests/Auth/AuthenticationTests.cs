using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Diva.Api.Features.Account;
using Microsoft.IdentityModel.Tokens;

namespace Diva.Tests.Auth;

public class AuthenticationTests(DivaApiFactory factory) : IClassFixture<DivaApiFactory>
{
    private Task<HttpResponseMessage> GetAsync(string url, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task Health_is_public()
    {
        var response = await GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_token_is_unauthorized()
    {
        var response = await GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_token_returns_the_user()
    {
        var token = factory.Jwt.CreateToken(subject: "user-123", email: "owner@example.com");

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(new MeResponse("user-123", "owner@example.com"), me);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var token = factory.Jwt.CreateToken(expires: DateTime.UtcNow.AddMinutes(-30));

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_for_another_audience_is_rejected()
    {
        var token = factory.Jwt.CreateToken(audience: "some-other-app");

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_from_another_issuer_is_rejected()
    {
        var token = factory.Jwt.CreateToken(issuer: "https://evil.example.com/auth/v1");

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        // The attacker even reuses our key id, but cannot produce a valid signature without the private key.
        using var attacker = new TestJwtIssuer(TestJwtIssuer.DefaultKeyId);
        var token = attacker.CreateToken();

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_hs256_is_rejected()
    {
        var secret = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) { KeyId = TestJwtIssuer.DefaultKeyId };
        var token = factory.Jwt.CreateToken(
            credentials: new SigningCredentials(secret, SecurityAlgorithms.HmacSha256));

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unsigned_token_with_alg_none_is_rejected()
    {
        static string B64(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var token =
            B64("""{"alg":"none","typ":"JWT"}""") + "." +
            B64($$"""{"sub":"attacker","iss":"{{TestJwtIssuer.Issuer}}","aud":"{{TestJwtIssuer.Audience}}","exp":{{exp}}}""") + ".";

        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_endpoint_paths_do_not_leak_data_without_login()
    {
        // Not mapped at all, so 404 is fine; the point is that nothing returns 200 for anonymous callers.
        var response = await GetAsync("/api/customers");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
