using System.Net;
using System.Net.Http.Json;
using Diva.Api.Domain;
using Diva.Api.Features.Services;
using Diva.Tests.Database;
using Microsoft.AspNetCore.Mvc;

namespace Diva.Tests.Features;

public class ServiceEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    private static readonly Guid[] SeedIds =
    [
        Guid.Parse("00000000-0000-0000-0000-000000000101"),
        Guid.Parse("00000000-0000-0000-0000-000000000102"),
        Guid.Parse("00000000-0000-0000-0000-000000000103"),
        Guid.Parse("00000000-0000-0000-0000-000000000104"),
        Guid.Parse("00000000-0000-0000-0000-000000000105"),
        Guid.Parse("00000000-0000-0000-0000-000000000106"),
    ];

    private static string UniqueName() => $"Jasa {Guid.NewGuid():N}";

    private async Task<ServiceResponse> CreateAsync(HttpClient client, string name, decimal price = 12_500m)
    {
        var response = await client.PostAsJsonAsync("/api/services", new { name, unit = "Kg", price });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ServiceResponse>(TestJson.Options))!;
    }

    [PostgresFact]
    public async Task Requires_login()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/api/services");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task List_contains_the_seeded_services()
    {
        var services = await api.CreateClient().GetFromJsonAsync<List<ServiceResponse>>("/api/services", TestJson.Options);

        Assert.All(SeedIds, id => Assert.Contains(services!, s => s.Id == id));
        var cuciKiloan = services!.Single(s => s.Id == SeedIds[0]);
        Assert.Equal(("Cuci Kiloan", ServiceUnit.Kg, 7_000m), (cuciKiloan.Name, cuciKiloan.Unit, cuciKiloan.Price));
    }

    [PostgresFact]
    public async Task Units_are_sent_as_text_in_json()
    {
        var json = await api.CreateClient().GetStringAsync($"/api/services/{SeedIds[0]}");

        Assert.Contains("\"unit\":\"Kg\"", json);
    }

    [PostgresFact]
    public async Task Create_then_get_returns_the_same_service()
    {
        var client = api.CreateClient();
        var name = UniqueName();

        var response = await client.PostAsJsonAsync("/api/services", new { name = $"  {name}  ", unit = "Pcs", price = 15_000.50m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ServiceResponse>(TestJson.Options);
        Assert.Equal($"/api/services/{created!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(new ServiceResponse(created.Id, name, ServiceUnit.Pcs, 15_000.50m, true), created);

        var fetched = await client.GetFromJsonAsync<ServiceResponse>($"/api/services/{created.Id}", TestJson.Options);
        Assert.Equal(created, fetched);
    }

    [PostgresFact]
    public async Task Invalid_service_returns_400_with_an_error_per_field()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = " ", price = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["name", "price", "unit"], problem!.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Price_with_more_than_two_decimals_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), unit = "Kg", price = 1.234m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Price_above_the_maximum_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), unit = "Kg", price = 100_000_000.01m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Unknown_unit_text_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), unit = "Liter", price = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Unit_as_a_number_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), unit = 1, price = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Update_changes_the_service()
    {
        var client = api.CreateClient();
        var service = await CreateAsync(client, UniqueName());
        var newName = UniqueName();

        var response = await client.PutAsJsonAsync($"/api/services/{service.Id}", new { name = newName, unit = "M2", price = 20_000 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await client.GetFromJsonAsync<ServiceResponse>($"/api/services/{service.Id}", TestJson.Options);
        Assert.Equal(new ServiceResponse(service.Id, newName, ServiceUnit.M2, 20_000m, true), fetched);
    }

    [PostgresFact]
    public async Task Delete_deactivates_instead_of_removing()
    {
        var client = api.CreateClient();
        var service = await CreateAsync(client, UniqueName());

        var delete = await client.DeleteAsync($"/api/services/{service.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var active = await client.GetFromJsonAsync<List<ServiceResponse>>("/api/services", TestJson.Options);
        Assert.DoesNotContain(active!, s => s.Id == service.Id);
        var all = await client.GetFromJsonAsync<List<ServiceResponse>>("/api/services?includeInactive=true", TestJson.Options);
        Assert.Contains(all!, s => s.Id == service.Id && !s.IsActive);
        var fetched = await client.GetFromJsonAsync<ServiceResponse>($"/api/services/{service.Id}", TestJson.Options);
        Assert.False(fetched!.IsActive);
    }

    [PostgresFact]
    public async Task Deactivated_service_can_be_reactivated_with_update()
    {
        var client = api.CreateClient();
        var service = await CreateAsync(client, UniqueName());
        await client.DeleteAsync($"/api/services/{service.Id}");

        var response = await client.PutAsJsonAsync(
            $"/api/services/{service.Id}", new { name = service.Name, unit = "Kg", price = service.Price, isActive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var active = await client.GetFromJsonAsync<List<ServiceResponse>>("/api/services", TestJson.Options);
        Assert.Contains(active!, s => s.Id == service.Id);
    }

    [PostgresFact]
    public async Task Unknown_id_returns_404_for_get_update_and_delete()
    {
        var client = api.CreateClient();
        var url = $"/api/services/{Guid.NewGuid()}";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(url, new { name = "x", unit = "Kg", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url)).StatusCode);
    }
}
