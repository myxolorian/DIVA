using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diva.Api.Domain;
using Diva.Api.Features.Customers;
using Diva.Api.Features.Orders;
using Diva.Api.Features.Outlet;
using Diva.Api.Features.Receipts;
using Diva.Tests.Auth;
using Diva.Tests.Database;
using Microsoft.AspNetCore.Mvc;

namespace Diva.Tests.Features;

public class ReceiptEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    private static readonly Guid CuciKeringSetrika = Guid.Parse("00000000-0000-0000-0000-000000000201");
    private static readonly Guid KebayaPayet = Guid.Parse("00000000-0000-0000-0000-000000000219");

    private async Task<OrderResponse> CreateOrderAsync()
    {
        var client = api.CreateClient();
        var customerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Ibu Sari",
            phone = $"0812{Random.Shared.Next(10_000_000, 99_999_999)}",
            address = "Jl. Rahasia 1",
        });
        var customer = (await customerResponse.Content.ReadFromJsonAsync<CustomerResponse>())!;

        var orderResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = customer.Id,
            notes = "Pisahkan baju putih",
            items = new object[]
            {
                new { serviceId = CuciKeringSetrika, qty = 2 },
                new { serviceId = KebayaPayet, qty = 1, unitPrice = 100_000 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        return (await orderResponse.Content.ReadFromJsonAsync<OrderResponse>(TestJson.Options))!;
    }

    [PostgresFact]
    public async Task Receipt_is_readable_without_login_through_the_token()
    {
        var order = await CreateOrderAsync();

        var response = await api.CreateAnonymousClient().GetAsync($"/api/public/receipts/{order.PublicToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<ReceiptResponse>(TestJson.Options))!;
        Assert.Equal((order.OrderNumber, 121_000m, "Ibu Sari"), (receipt.OrderNumber, receipt.Total, receipt.CustomerName));
        Assert.Equal(["Cuci Kering Setrika", "Dress Pesta/Kebaya Payet"], receipt.Items.Select(i => i.ServiceName));
        Assert.Equal((2m, 3m, 21_000m), (receipt.Items[0].Qty, receipt.Items[0].BilledQty, receipt.Items[0].Subtotal));
        Assert.Equal("DIVA Laundry", receipt.Outlet.Name);
        Assert.Equal($"/api/public/receipts/{order.PublicToken}/pdf", receipt.PdfPath);
    }

    [PostgresFact]
    public async Task Receipt_hides_the_phone_and_leaves_out_the_address_and_ids()
    {
        var order = await CreateOrderAsync();

        var json = await api.CreateAnonymousClient().GetStringAsync($"/api/public/receipts/{order.PublicToken}");

        var receipt = JsonSerializer.Deserialize<ReceiptResponse>(json, TestJson.Options)!;
        Assert.Equal(ReceiptFormat.MaskPhone(order.Customer.Phone), receipt.CustomerPhone);
        Assert.DoesNotContain(order.Customer.Phone, json);
        Assert.DoesNotContain("Jl. Rahasia", json);
        Assert.DoesNotContain(order.Customer.Id.ToString(), json);
        Assert.DoesNotContain(order.Id.ToString(), json);
    }

    [PostgresFact]
    public async Task Receipt_is_never_cached_or_indexed()
    {
        var order = await CreateOrderAsync();

        var response = await api.CreateAnonymousClient().GetAsync($"/api/public/receipts/{order.PublicToken}");

        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("noindex", response.Headers.GetValues("X-Robots-Tag").Single());
    }

    [PostgresFact]
    public async Task Receipt_shows_the_latest_status_and_payment()
    {
        var order = await CreateOrderAsync();
        await api.CreateClient().PatchAsJsonAsync($"/api/orders/{order.Id}/payment", new { paymentStatus = "Lunas" });

        var receipt = await api.CreateAnonymousClient()
            .GetFromJsonAsync<ReceiptResponse>($"/api/public/receipts/{order.PublicToken}", TestJson.Options);

        Assert.Equal(PaymentStatus.Lunas, receipt!.PaymentStatus);
    }

    [PostgresFact]
    public async Task Unknown_or_malformed_tokens_return_404()
    {
        var client = api.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/public/receipts/AAAAAAAAAAAAAAAAAAAAAA")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/public/receipts/too-short")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/public/receipts/AAAAAAAAAAAAAAAAAAAAAA/pdf")).StatusCode);
    }

    [PostgresFact]
    public async Task Pdf_is_a_downloadable_pdf_named_after_the_order()
    {
        var order = await CreateOrderAsync();

        var response = await api.CreateAnonymousClient().GetAsync($"/api/public/receipts/{order.PublicToken}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"{order.OrderNumber}.pdf", response.Content.Headers.ContentDisposition?.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1_000);
    }

    [PostgresFact]
    public async Task Shared_link_serves_the_receipt_page()
    {
        var order = await CreateOrderAsync();

        var response = await api.CreateAnonymousClient().GetAsync($"/r/{order.PublicToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("/assets/js/receipt.js", await response.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task Frontend_assets_are_public()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/assets/js/receipt.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task Outlet_profile_can_be_read_and_changed_and_shows_on_receipts()
    {
        var client = api.CreateClient();
        var order = await CreateOrderAsync();

        var update = await client.PutAsJsonAsync("/api/outlet", new
        {
            name = " DIVA Laundry Pusat ",
            address = "Jl. Kenanga 10",
            phone = "0812-0000-1111",
            receiptFooter = "Terima kasih!",
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var outlet = await client.GetFromJsonAsync<OutletResponse>("/api/outlet");
        Assert.Equal(new OutletResponse("DIVA Laundry Pusat", "Jl. Kenanga 10", "081200001111", "Terima kasih!"), outlet);
        var receipt = await api.CreateAnonymousClient()
            .GetFromJsonAsync<ReceiptResponse>($"/api/public/receipts/{order.PublicToken}", TestJson.Options);
        Assert.Equal(new ReceiptOutlet("DIVA Laundry Pusat", "Jl. Kenanga 10", "081200001111", "Terima kasih!"), receipt!.Outlet);

        // Put the default back for the other tests in this class.
        await client.PutAsJsonAsync("/api/outlet", new { name = "DIVA Laundry" });
    }

    [PostgresFact]
    public async Task Outlet_requires_a_name_and_a_valid_phone()
    {
        var response = await api.CreateClient().PutAsJsonAsync("/api/outlet", new { name = "", phone = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["name", "phone"], problem!.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Outlet_requires_login()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/api/outlet");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class ReceiptRateLimitTests
{
    [Fact]
    public async Task Public_receipts_are_rate_limited()
    {
        // Malformed tokens are answered without a database, which is enough to count requests.
        using var factory = new DivaApiFactory(
            "Development", new Dictionary<string, string?> { [ReceiptEndpoints.RateLimitSetting] = "3" });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.GetAsync("/api/public/receipts/x")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests], statuses);
    }
}
