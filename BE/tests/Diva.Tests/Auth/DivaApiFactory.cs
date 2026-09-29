using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Diva.Tests.Auth;

/// <summary>
/// Starts the real API in memory. Login is real too (JwtBearer), except that the public keys come
/// from <see cref="Jwt"/> instead of being downloaded from Supabase.
/// </summary>
public sealed class DivaApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly IReadOnlyDictionary<string, string?>? _settings;

    // xUnit builds class fixtures through the one public constructor, which must have no parameters.
    public DivaApiFactory() : this("Development", null)
    {
    }

    internal DivaApiFactory(string environment, IReadOnlyDictionary<string, string?>? settings)
    {
        _environment = environment;
        _settings = settings;
    }

    public TestJwtIssuer Jwt { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        var values = new Dictionary<string, string?>
        {
            // The database is never opened by these tests, but Program insists on a connection string.
            ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["Supabase:Url"] = TestJwtIssuer.ProjectUrl,
        };
        foreach (var (key, value) in _settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        foreach (var (key, value) in values)
        {
            // UseSetting: visible while Program.cs is still running. AddInMemoryCollection: wins over appsettings.json.
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values));

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(Jwt.CreateConfiguration())));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Jwt.Dispose();
        }

        base.Dispose(disposing);
    }
}
