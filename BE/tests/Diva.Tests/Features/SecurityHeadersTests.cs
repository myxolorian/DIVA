using System.Net;
using Diva.Api.Features.Receipts;
using Diva.Tests.Auth;

namespace Diva.Tests.Features;

public class SecurityHeadersTests
{
    private const string ReceiptUrl = "/api/public/receipts/doesnotexist0000000000";

    private static DivaApiFactory Factory(string environment = "Development", bool trustProxy = false) =>
        new(environment, new Dictionary<string, string?>
        {
            ["Proxy:TrustForwardedHeaders"] = trustProxy ? "true" : null,
            [ReceiptEndpoints.RateLimitSetting] = "2",
        });

    private static HttpClient Client(DivaApiFactory factory, string baseAddress = "http://diva.example.com")
    {
        var client = factory.CreateClient();
        client.BaseAddress = new Uri(baseAddress);
        return client;
    }

    private static Task<HttpResponseMessage> GetFromIpAsync(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ReceiptUrl);
        request.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(request);
    }

    [Theory]
    [InlineData("/login.html")]
    [InlineData("/health")]
    public async Task Pages_and_api_send_the_security_headers(string url)
    {
        using var factory = Factory();

        var response = await Client(factory).GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self';", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        // The login page talks to Supabase Auth, so its address must be allowed (and nothing else).
        Assert.Contains($"connect-src 'self' {TestJwtIssuer.ProjectUrl};", csp);
    }

    [Fact]
    public async Task Behind_the_proxy_each_visitor_has_their_own_receipt_rate_limit()
    {
        using var factory = Factory(trustProxy: true);
        var client = Client(factory);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await GetFromIpAsync(client, "203.0.113.1")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await GetFromIpAsync(client, "203.0.113.1")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await GetFromIpAsync(client, "203.0.113.1")).StatusCode);

        // Another customer opening their receipt is not blocked by the first one.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await GetFromIpAsync(client, "198.51.100.7")).StatusCode);
    }

    [Fact]
    public async Task Without_a_trusted_proxy_a_forged_forwarded_ip_does_not_escape_the_limit()
    {
        using var factory = Factory(trustProxy: false);
        var client = Client(factory);

        await GetFromIpAsync(client, "203.0.113.1");
        await GetFromIpAsync(client, "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await GetFromIpAsync(client, "203.0.113.3")).StatusCode);
    }

    [Fact]
    public async Task Hsts_is_sent_in_production_over_https_as_reported_by_the_proxy()
    {
        using var trusted = Factory("Production", trustProxy: true);
        using var untrusted = Factory("Production", trustProxy: false);

        HttpRequestMessage Https()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add("X-Forwarded-Proto", "https");
            return request;
        }

        var viaProxy = await Client(trusted).SendAsync(Https());
        var notTrusted = await Client(untrusted).SendAsync(Https());

        Assert.Contains("max-age=", Assert.Single(viaProxy.Headers.GetValues("Strict-Transport-Security")));
        Assert.False(notTrusted.Headers.Contains("Strict-Transport-Security")); // the request really was plain http
    }

    [Fact]
    public async Task Hsts_is_not_sent_in_development()
    {
        using var factory = Factory("Development");

        var response = await Client(factory, "https://diva.example.com").GetAsync("/health");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
