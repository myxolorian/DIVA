using System.Net;
using System.Net.Http.Json;
using Diva.Api.Features.Account;
using Microsoft.Extensions.Options;

namespace Diva.Tests.Auth;

public class DevBypassTests
{
    private static readonly Dictionary<string, string?> BypassOn = new() { ["Auth:DevBypass"] = "true" };

    [Fact]
    public async Task In_development_the_bypass_logs_everyone_in_as_the_dev_user()
    {
        using var factory = new DivaApiFactory("Development", BypassOn);

        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("dev@diva.local", me!.Email);
    }

    [Fact]
    public void Outside_development_the_bypass_makes_the_app_refuse_to_start()
    {
        using var factory = new DivaApiFactory("Production", BypassOn);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Auth:DevBypass", error.ToString());
    }

    [Fact]
    public void Without_the_bypass_a_broken_supabase_url_makes_the_app_refuse_to_start()
    {
        var settings = new Dictionary<string, string?> { ["Supabase:Url"] = "http://not-https.example.com" };
        using var factory = new DivaApiFactory("Development", settings);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(nameof(OptionsValidationException), error.ToString());
    }
}
