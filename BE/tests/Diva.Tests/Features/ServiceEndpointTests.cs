using System.Net;
using System.Net.Http.Json;
using Diva.Api.Domain;
using static Diva.Api.Data.Configurations.LaundryServiceConfiguration;
using Diva.Api.Features.Services;
using Diva.Tests.Database;
using Microsoft.AspNetCore.Mvc;

namespace Diva.Tests.Features;

public class ServiceEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    private static readonly Guid CuciKeringSetrika = Guid.Parse("00000000-0000-0000-0000-000000000201");
    private static readonly Guid KebayaPayet = Guid.Parse("00000000-0000-0000-0000-000000000219");

    private static string UniqueName() => $"Jasa {Guid.NewGuid():N}";

    private async Task<ServiceResponse> CreateAsync(HttpClient client, string name, decimal price = 12_500m)
    {
        var response = await client.PostAsJsonAsync("/api/services", new { name, category = "Tes", unit = "Kg", price });
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
    public async Task List_contains_the_price_list_in_printed_order()
    {
        var services = await api.CreateClient().GetFromJsonAsync<List<ServiceResponse>>("/api/services", TestJson.Options);

        var seeded = services!.Where(s => PriceList.Services.Any(p => p.Id == s.Id)).ToList();
        Assert.Equal(PriceList.Services.OrderBy(p => p.SortOrder).Select(p => p.Id), seeded.Select(s => s.Id));

        var kiloan = services!.Single(s => s.Id == CuciKeringSetrika);
        Assert.Equal(
            ("Cuci Kering Setrika", "Laundry Kiloan", ServiceUnit.Kg, 7_000m, (decimal?)3m, (decimal?)null),
            (kiloan.Name, kiloan.Category, kiloan.Unit, kiloan.Price, kiloan.MinQty, kiloan.MaxPrice));
        var kebaya = services!.Single(s => s.Id == KebayaPayet);
        Assert.Equal((60_000m, (decimal?)150_000m), (kebaya.Price, kebaya.MaxPrice));
    }

    [PostgresFact]
    public async Task Units_are_sent_as_text_in_json()
    {
        var json = await api.CreateClient().GetStringAsync($"/api/services/{CuciKeringSetrika}");

        Assert.Contains("\"unit\":\"Kg\"", json);
    }

    [PostgresFact]
    public async Task Create_then_get_returns_the_same_service()
    {
        var client = api.CreateClient();
        var name = UniqueName();

        var response = await client.PostAsJsonAsync("/api/services", new
        {
            name = $"  {name}  ",
            category = " Sepatu ",
            unit = "Pcs",
            price = 15_000.50m,
            maxPrice = 30_000m,
            minQty = 2,
            sortOrder = 5,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ServiceResponse>(TestJson.Options);
        Assert.Equal($"/api/services/{created!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(new ServiceResponse(created.Id, name, "Sepatu", 5, ServiceUnit.Pcs, 15_000.50m, 30_000m, 2m, true), created);

        var fetched = await client.GetFromJsonAsync<ServiceResponse>($"/api/services/{created.Id}", TestJson.Options);
        Assert.Equal(created, fetched);
    }

    [PostgresFact]
    public async Task Invalid_service_returns_400_with_an_error_per_field()
    {
        var response = await api.CreateClient().PostAsJsonAsync(
            "/api/services", new { name = " ", price = 0, maxPrice = -1, minQty = 0, sortOrder = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["category", "maxPrice", "minQty", "name", "price", "sortOrder", "unit"], problem!.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Price_with_more_than_two_decimals_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), category = "Tes", unit = "Kg", price = 1.234m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Price_above_the_maximum_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), category = "Tes", unit = "Kg", price = 100_000_000.01m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Unknown_unit_text_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), category = "Tes", unit = "Liter", price = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Unit_as_a_number_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/services", new { name = UniqueName(), category = "Tes", unit = 1, price = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Max_price_must_be_above_the_price()
    {
        var response = await api.CreateClient().PostAsJsonAsync(
            "/api/services", new { name = UniqueName(), category = "Tes", unit = "Pcs", price = 60_000, maxPrice = 60_000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["maxPrice"], problem!.Errors.Keys);
    }

    [PostgresFact]
    public async Task New_service_without_sort_order_goes_to_the_end_of_the_list()
    {
        var client = api.CreateClient();

        var service = await CreateAsync(client, UniqueName());

        var services = await client.GetFromJsonAsync<List<ServiceResponse>>("/api/services", TestJson.Options);
        Assert.Equal(service.Id, services!.Last().Id);
        Assert.True(service.SortOrder > PriceList.Services.Max(p => p.SortOrder));
    }

    [PostgresFact]
    public async Task Update_changes_the_service()
    {
        var client = api.CreateClient();
        var service = await CreateAsync(client, UniqueName());
        var newName = UniqueName();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{service.Id}", new { name = newName, category = "Karpet", unit = "M2", price = 20_000, minQty = 4 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await client.GetFromJsonAsync<ServiceResponse>($"/api/services/{service.Id}", TestJson.Options);
        // sortOrder was left out, so it keeps its old value.
        Assert.Equal(new ServiceResponse(service.Id, newName, "Karpet", service.SortOrder, ServiceUnit.M2, 20_000m, null, 4m, true), fetched);
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
            $"/api/services/{service.Id}", new { name = service.Name, category = service.Category, unit = "Kg", price = service.Price, isActive = true });

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
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(url, new { name = "x", category = "Tes", unit = "Kg", price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url)).StatusCode);
    }
}
