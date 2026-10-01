using System.Net;
using System.Net.Http.Json;
using Diva.Api.Features.AppConfig;
using Diva.Tests.Auth;

namespace Diva.Tests.Features;

public class AppConfigEndpointTests
{
    private static readonly Dictionary<string, string?> Settings = new()
    {
        // Pasted with the REST suffix on purpose: the login page must still get the bare project URL.
        ["Supabase:Url"] = TestJwtIssuer.ProjectUrl + "/rest/v1/",
        ["Supabase:PublishableKey"] = "sb_publishable_test",
    };

    [Fact]
    public async Task Config_is_public_and_gives_the_login_page_what_it_needs()
    {
        using var factory = new DivaApiFactory("Development", Settings);

        var response = await factory.CreateClient().GetAsync("/api/public/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<AppConfigResponse>();
        Assert.Equal(new AppConfigResponse(TestJwtIssuer.ProjectUrl, "sb_publishable_test", DevBypass: false), config);
    }

    [Fact]
    public async Task Config_tells_the_login_page_when_the_dev_bypass_is_on()
    {
        using var factory = new DivaApiFactory("Development", new Dictionary<string, string?>(Settings) { ["Auth:DevBypass"] = "true" });

        var config = await factory.CreateClient().GetFromJsonAsync<AppConfigResponse>("/api/public/config");

        Assert.True(config!.DevBypass);
    }

    [Fact]
    public async Task Pages_are_public_and_revalidated_so_an_update_shows_up_straight_away()
    {
        using var factory = new DivaApiFactory("Development", Settings);

        var response = await factory.CreateClient().GetAsync("/login.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Home_page_is_the_frontend()
    {
        using var factory = new DivaApiFactory("Development", Settings);

        var response = await factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_frontend_says_so_instead_of_asking_for_a_login()
    {
        var emptyFolder = Directory.CreateTempSubdirectory("diva-no-fe-");
        try
        {
            using var factory = new DivaApiFactory("Development",
                new Dictionary<string, string?>(Settings) { ["Frontend:Path"] = emptyFolder.FullName });

            var response = await factory.CreateClient().GetAsync("/");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("Halaman DIVA belum ada di server", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            emptyFolder.Delete(recursive: true);
        }
    }
}
