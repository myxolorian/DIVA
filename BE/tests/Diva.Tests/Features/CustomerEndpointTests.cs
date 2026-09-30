using System.Net;
using System.Net.Http.Json;
using Diva.Api.Features.Common;
using Diva.Api.Features.Customers;
using Diva.Tests.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Diva.Tests.Features;

public class CustomerEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    private static int _phoneCounter;

    /// <summary>A phone number no other test uses, so the duplicate check never interferes.</summary>
    private static string UniquePhone() =>
        $"0812{Interlocked.Increment(ref _phoneCounter):D4}{Random.Shared.Next(1000, 9999)}";

    private static string UniqueName(string prefix = "Customer") => $"{prefix} {Guid.NewGuid():N}";

    private async Task<CustomerResponse> CreateAsync(HttpClient client, string? name = null, string? phone = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/customers", new { name = name ?? UniqueName(), phone = phone ?? UniquePhone(), address = "Jl. Melati 1" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    private static Task<PagedResult<CustomerResponse>?> ListAsync(HttpClient client, string query) =>
        client.GetFromJsonAsync<PagedResult<CustomerResponse>>($"/api/customers?{query}");

    [PostgresFact]
    public async Task Requires_login()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task Create_trims_text_and_normalizes_the_phone()
    {
        var client = api.CreateClient();
        var name = UniqueName();

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            name = $"  {name}  ",
            phone = " 0899-1234 5678 ",
            address = "  Jl. Mawar 5  ",
            notes = "   ",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal((name, "089912345678", "Jl. Mawar 5", (string?)null), (created!.Name, created.Phone, created.Address, created.Notes));
        Assert.Equal($"/api/customers/{created.Id}", response.Headers.Location?.OriginalString);

        var fetched = await client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);
    }

    [PostgresFact]
    public async Task Invalid_customer_returns_400_with_an_error_per_field()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/customers", new
        {
            name = "",
            phone = "abc",
            address = new string('x', ValidCustomer.AddressMax + 1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["address", "name", "phone"], problem!.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Missing_phone_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/customers", new { name = UniqueName() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["phone"], problem!.Errors.Keys);
    }

    [PostgresFact]
    public async Task Same_phone_in_another_format_is_a_conflict()
    {
        var client = api.CreateClient();
        var phone = UniquePhone();
        var first = await CreateAsync(client, phone: phone);
        var formatted = $"{phone[..4]}-{phone[4..8]} {phone[8..]}";

        var response = await client.PostAsJsonAsync("/api/customers", new { name = UniqueName(), phone = formatted });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains(first.Name, problem!.Title);
        Assert.Equal(first.Id.ToString(), problem.Extensions["customerId"]?.ToString());
    }

    [PostgresFact]
    public async Task Phone_of_a_deleted_customer_can_be_reused()
    {
        var client = api.CreateClient();
        var phone = UniquePhone();
        var first = await CreateAsync(client, phone: phone);
        await client.DeleteAsync($"/api/customers/{first.Id}");

        var response = await client.PostAsJsonAsync("/api/customers", new { name = UniqueName(), phone });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [PostgresFact]
    public async Task Update_changes_the_customer_and_may_keep_its_own_phone()
    {
        var client = api.CreateClient();
        var customer = await CreateAsync(client);
        var newName = UniqueName();

        var response = await client.PutAsJsonAsync(
            $"/api/customers/{customer.Id}", new { name = newName, phone = customer.Phone, address = "Jl. Baru 9", notes = "Tanpa pewangi" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}");
        Assert.Equal((newName, customer.Phone, "Jl. Baru 9", "Tanpa pewangi"), (fetched!.Name, fetched.Phone, fetched.Address, fetched.Notes));
    }

    [PostgresFact]
    public async Task Update_to_another_customers_phone_is_a_conflict()
    {
        var client = api.CreateClient();
        var first = await CreateAsync(client);
        var second = await CreateAsync(client);

        var response = await client.PutAsJsonAsync($"/api/customers/{second.Id}", new { name = second.Name, phone = first.Phone });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [PostgresFact]
    public async Task Delete_is_a_soft_delete()
    {
        var client = api.CreateClient();
        var customer = await CreateAsync(client);

        var delete = await client.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/customers/{customer.Id}")).StatusCode);
        var list = await ListAsync(client, $"search={customer.Phone}");
        Assert.Empty(list!.Items);

        // The row is still in the database, only marked as deleted.
        await using var db = api.CreateDbContext();
        var row = await db.Customers.SingleAsync(c => c.Id == customer.Id);
        Assert.True(row.IsDeleted);
    }

    [PostgresFact]
    public async Task Deleted_customer_cannot_be_updated_or_deleted_again()
    {
        var client = api.CreateClient();
        var customer = await CreateAsync(client);
        await client.DeleteAsync($"/api/customers/{customer.Id}");

        var update = await client.PutAsJsonAsync($"/api/customers/{customer.Id}", new { name = "x", phone = UniquePhone() });
        var deleteAgain = await client.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [PostgresFact]
    public async Task Search_matches_part_of_the_name_ignoring_case()
    {
        var client = api.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..10];
        var customer = await CreateAsync(client, name: $"Ibu Sari {marker}");

        var list = await ListAsync(client, $"search=SARI%20{marker.ToUpperInvariant()}");

        Assert.Equal([customer.Id], list!.Items.Select(c => c.Id));
        Assert.Equal(1, list.Total);
    }

    [PostgresFact]
    public async Task Search_matches_the_phone_even_when_typed_with_separators()
    {
        var client = api.CreateClient();
        var customer = await CreateAsync(client);
        var typed = $"{customer.Phone[..4]}-{customer.Phone[4..8]}";

        var list = await ListAsync(client, $"search={Uri.EscapeDataString(typed)}");

        Assert.Contains(list!.Items, c => c.Id == customer.Id);
    }

    [PostgresFact]
    public async Task Search_treats_wildcards_literally()
    {
        var client = api.CreateClient();
        await CreateAsync(client);

        var list = await ListAsync(client, "search=%25"); // "%" would match everyone without escaping

        Assert.Empty(list!.Items);
    }

    [PostgresFact]
    public async Task List_is_paged_and_sorted_by_name()
    {
        var client = api.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..10];
        var c = await CreateAsync(client, name: $"{marker} C");
        var a = await CreateAsync(client, name: $"{marker} A");
        var b = await CreateAsync(client, name: $"{marker} B");

        var page1 = await ListAsync(client, $"search={marker}&pageSize=2&page=1");
        var page2 = await ListAsync(client, $"search={marker}&pageSize=2&page=2");

        Assert.Equal([a.Id, b.Id], page1!.Items.Select(x => x.Id));
        Assert.Equal((3, 1, 2), (page1.Total, page1.Page, page1.PageSize));
        Assert.Equal([c.Id], page2!.Items.Select(x => x.Id));
    }

    [PostgresFact]
    public async Task Out_of_range_paging_values_are_clamped()
    {
        var list = await ListAsync(api.CreateClient(), "page=0&pageSize=1000");

        Assert.Equal((1, PagedResult<CustomerResponse>.MaxPageSize), (list!.Page, list.PageSize));
    }

    [PostgresFact]
    public async Task Unknown_id_returns_404()
    {
        var response = await api.CreateClient().GetAsync($"/api/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Customer tidak ditemukan.", problem!.Title);
    }
}
