using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Diva.Api.Features.Customers;
using Diva.Api.Features.Orders;
using Diva.Api.Features.Services;
using Diva.Tests.Database;
using Microsoft.AspNetCore.Mvc;

namespace Diva.Tests.Features;

public class OrderEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    // From the seeded price list.
    private static readonly Guid CuciKeringSetrika = Guid.Parse("00000000-0000-0000-0000-000000000201"); // 7.000/kg, min 3
    private static readonly Guid KarpetTipis = Guid.Parse("00000000-0000-0000-0000-000000000211");       // 15.000/m2, min 4
    private static readonly Guid JasBlazer = Guid.Parse("00000000-0000-0000-0000-000000000216");         // 35.000/pcs
    private static readonly Guid KebayaPayet = Guid.Parse("00000000-0000-0000-0000-000000000219");       // 60.000-150.000

    private static int _phoneCounter;

    private static DateOnly TodayWib() => Wib.Today(TimeProvider.System);

    private async Task<CustomerResponse> CreateCustomerAsync(HttpClient client)
    {
        var phone = $"0813{Interlocked.Increment(ref _phoneCounter):D4}{Random.Shared.Next(1000, 9999)}";
        var response = await client.PostAsJsonAsync("/api/customers", new { name = $"Customer {Guid.NewGuid():N}", phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    private static async Task<OrderResponse> CreateOrderAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/orders", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(TestJson.Options))!;
    }

    private static Task<OrderResponse> CreateSimpleOrderAsync(HttpClient client, Guid customerId) =>
        CreateOrderAsync(client, new { customerId, items = new[] { new { serviceId = JasBlazer, qty = 1 } } });

    private static async Task<ValidationProblemDetails> PostInvalidAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/orders", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
    }

    [PostgresFact]
    public async Task Requires_login()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task Create_calculates_every_line_on_the_server()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = customer.Id,
            notes = "  Pisahkan baju putih  ",
            dueDate = TodayWib().AddDays(2),
            items = new object[]
            {
                new { serviceId = CuciKeringSetrika, qty = 2 },            // below min 3 kg -> 3 x 7.000
                new { serviceId = KebayaPayet, qty = 1, unitPrice = 100_000 }, // price typed by the cashier
                new { serviceId = KarpetTipis, qty = 5.5 },                // above min 4 m2 -> 5,5 x 15.000
                new { serviceId = JasBlazer, qty = 2 },                    // 2 x 35.000
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>(TestJson.Options))!;
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location?.OriginalString);

        Assert.Equal(
            [
                new OrderItemResponse(1, CuciKeringSetrika, "Cuci Kering Setrika", ServiceUnit.Kg, 2m, 3m, 3m, 7_000m, 21_000m),
                new OrderItemResponse(2, KebayaPayet, "Dress Pesta/Kebaya Payet", ServiceUnit.Pcs, 1m, null, 1m, 100_000m, 100_000m),
                new OrderItemResponse(3, KarpetTipis, "Karpet Tipis", ServiceUnit.M2, 5.5m, 4m, 5.5m, 15_000m, 82_500m),
                new OrderItemResponse(4, JasBlazer, "Jas/Blazer", ServiceUnit.Pcs, 2m, null, 2m, 35_000m, 70_000m),
            ],
            order.Items);
        Assert.Equal(273_500m, order.Total);
        Assert.Equal((OrderStatus.Baru, PaymentStatus.BelumLunas), (order.Status, order.PaymentStatus));
        Assert.Equal(("Pisahkan baju putih", TodayWib().AddDays(2)), (order.Notes, order.DueDate));
        Assert.Equal((customer.Id, customer.Name), (order.Customer.Id, order.Customer.Name));
        Assert.Matches(new Regex($"^DIV-{Wib.ToWib(order.CreatedAt):yyMMdd}-\\d{{4,}}$"), order.OrderNumber);
        Assert.Matches(new Regex("^[A-Za-z0-9_-]{22}$"), order.PublicToken);
        Assert.Equal($"/r/{order.PublicToken}", order.ReceiptPath);
    }

    [PostgresFact]
    public async Task Get_returns_the_same_order_with_items_in_entry_order()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var created = await CreateOrderAsync(client, new
        {
            customerId = customer.Id,
            items = new object[]
            {
                new { serviceId = JasBlazer, qty = 1 },
                new { serviceId = CuciKeringSetrika, qty = 4 },
                new { serviceId = JasBlazer, qty = 3 },
            },
        });

        var fetched = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{created.Id}", TestJson.Options);

        Assert.Equal(created.Items, fetched!.Items);
        Assert.Equal([1, 2, 3], fetched.Items.Select(i => i.LineNo));
        Assert.Equal((created.OrderNumber, created.Total, created.PublicToken), (fetched.OrderNumber, fetched.Total, fetched.PublicToken));
    }

    [PostgresFact]
    public async Task Order_numbers_are_unique_and_increasing()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);

        var first = await CreateSimpleOrderAsync(client, customer.Id);
        var second = await CreateSimpleOrderAsync(client, customer.Id);

        static long Running(string number) => long.Parse(number.Split('-')[2]);
        Assert.True(Running(second.OrderNumber) > Running(first.OrderNumber));
        Assert.NotEqual(first.PublicToken, second.PublicToken);
    }

    [PostgresFact]
    public async Task Changing_a_service_price_later_does_not_change_existing_orders()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var serviceResponse = await client.PostAsJsonAsync(
            "/api/services", new { name = $"Jasa {Guid.NewGuid():N}", category = "Tes", unit = "Pcs", price = 10_000 });
        var service = (await serviceResponse.Content.ReadFromJsonAsync<ServiceResponse>(TestJson.Options))!;
        var order = await CreateOrderAsync(client, new { customerId = customer.Id, items = new[] { new { serviceId = service.Id, qty = 2 } } });

        await client.PutAsJsonAsync($"/api/services/{service.Id}", new { name = "Nama baru", category = "Tes", unit = "Pcs", price = 99_000 });

        var fetched = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", TestJson.Options);
        var line = Assert.Single(fetched!.Items);
        Assert.Equal((service.Name, 10_000m, 20_000m), (line.ServiceName, line.UnitPrice, line.Subtotal));
        Assert.Equal(20_000m, fetched.Total);
    }

    [PostgresFact]
    public async Task Missing_customer_and_items_are_reported()
    {
        var problem = await PostInvalidAsync(api.CreateClient(), new { items = Array.Empty<object>() });

        Assert.Equal(["customerId", "items"], problem.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Deleted_or_unknown_customer_is_rejected()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        await client.DeleteAsync($"/api/customers/{customer.Id}");
        var items = new[] { new { serviceId = JasBlazer, qty = 1 } };

        var deleted = await PostInvalidAsync(client, new { customerId = customer.Id, items });
        var unknown = await PostInvalidAsync(client, new { customerId = Guid.NewGuid(), items });

        Assert.Equal(["customerId"], deleted.Errors.Keys);
        Assert.Equal(["customerId"], unknown.Errors.Keys);
    }

    [PostgresFact]
    public async Task Inactive_or_unknown_service_is_rejected_per_line()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var serviceResponse = await client.PostAsJsonAsync(
            "/api/services", new { name = $"Jasa {Guid.NewGuid():N}", category = "Tes", unit = "Pcs", price = 10_000 });
        var service = (await serviceResponse.Content.ReadFromJsonAsync<ServiceResponse>(TestJson.Options))!;
        await client.DeleteAsync($"/api/services/{service.Id}");

        var problem = await PostInvalidAsync(client, new
        {
            customerId = customer.Id,
            items = new[]
            {
                new { serviceId = JasBlazer, qty = 1 },
                new { serviceId = service.Id, qty = 1 },
                new { serviceId = Guid.NewGuid(), qty = 1 },
            },
        });

        Assert.Equal(["items[1].serviceId", "items[2].serviceId"], problem.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Invalid_quantities_and_prices_are_reported_per_line()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);

        var problem = await PostInvalidAsync(client, new
        {
            customerId = customer.Id,
            items = new object[]
            {
                new { serviceId = JasBlazer, qty = 1.5 },                         // pcs must be whole
                new { serviceId = KebayaPayet, qty = 1 },                         // range price missing
                new { serviceId = KebayaPayet, qty = 1, unitPrice = 200_000 },    // above the range
                new { serviceId = CuciKeringSetrika, qty = 3, unitPrice = 1 },    // fixed price cannot be overridden
                new { serviceId = CuciKeringSetrika, qty = 0 },
            },
        });

        Assert.Equal(
            ["items[0].qty", "items[1].unitPrice", "items[2].unitPrice", "items[3].unitPrice", "items[4].qty"],
            problem.Errors.Keys.Order());
    }

    [PostgresFact]
    public async Task Due_date_in_the_past_is_rejected_but_today_is_fine()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var items = new[] { new { serviceId = JasBlazer, qty = 1 } };

        var past = await PostInvalidAsync(client, new { customerId = customer.Id, items, dueDate = TodayWib().AddDays(-1) });
        var today = await CreateOrderAsync(client, new { customerId = customer.Id, items, dueDate = TodayWib() });

        Assert.Equal(["dueDate"], past.Errors.Keys);
        Assert.Equal(TodayWib(), today.DueDate);
    }

    [PostgresFact]
    public async Task Payment_status_can_be_set_when_creating()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);

        var order = await CreateOrderAsync(client, new
        {
            customerId = customer.Id,
            paymentStatus = "Lunas",
            items = new[] { new { serviceId = JasBlazer, qty = 1 } },
        });

        Assert.Equal(PaymentStatus.Lunas, order.PaymentStatus);
    }

    [PostgresFact]
    public async Task Status_and_payment_can_be_updated()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var order = await CreateSimpleOrderAsync(client, customer.Id);

        var status = await client.PatchAsJsonAsync($"/api/orders/{order.Id}/status", new { status = "Selesai" });
        var payment = await client.PatchAsJsonAsync($"/api/orders/{order.Id}/payment", new { paymentStatus = "Lunas" });

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        var fetched = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", TestJson.Options);
        Assert.Equal((OrderStatus.Selesai, PaymentStatus.Lunas), (fetched!.Status, fetched.PaymentStatus));
    }

    [PostgresFact]
    public async Task Invalid_status_updates_are_rejected()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var order = await CreateSimpleOrderAsync(client, customer.Id);

        var unknown = await client.PatchAsJsonAsync($"/api/orders/{order.Id}/status", new { status = "Hilang" });
        var missing = await client.PatchAsJsonAsync($"/api/orders/{order.Id}/status", new { });
        var missingPayment = await client.PatchAsJsonAsync($"/api/orders/{order.Id}/payment", new { });

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingPayment.StatusCode);
    }

    [PostgresFact]
    public async Task Unknown_order_returns_404()
    {
        var client = api.CreateClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/orders/{id}/status", new { status = "Selesai" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/orders/{id}/payment", new { paymentStatus = "Lunas" })).StatusCode);
    }

    [PostgresFact]
    public async Task List_filters_by_customer_status_search_and_date()
    {
        var client = api.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var older = await CreateSimpleOrderAsync(client, customer.Id);
        var newer = await CreateSimpleOrderAsync(client, customer.Id);
        await client.PatchAsJsonAsync($"/api/orders/{older.Id}/status", new { status = "Diproses" });

        async Task<PagedResult<OrderSummaryResponse>> ListAsync(string query) =>
            (await client.GetFromJsonAsync<PagedResult<OrderSummaryResponse>>($"/api/orders?customerId={customer.Id}&{query}", TestJson.Options))!;

        var all = await ListAsync("");
        Assert.Equal([newer.Id, older.Id], all.Items.Select(o => o.Id)); // newest first
        Assert.Equal((customer.Name, 35_000m, 1), (all.Items[0].CustomerName, all.Items[0].Total, all.Items[0].ItemCount));

        Assert.Equal([older.Id], (await ListAsync("status=Diproses")).Items.Select(o => o.Id));
        Assert.Equal([newer.Id], (await ListAsync("status=Baru")).Items.Select(o => o.Id));
        Assert.Equal([older.Id], (await ListAsync($"search={older.OrderNumber}")).Items.Select(o => o.Id));
        Assert.Equal(2, (await ListAsync($"search={customer.Phone}")).Total);

        var today = TodayWib().ToString("yyyy-MM-dd");
        var tomorrow = TodayWib().AddDays(1).ToString("yyyy-MM-dd");
        Assert.Equal(2, (await ListAsync($"from={today}&to={today}")).Total);
        Assert.Equal(0, (await ListAsync($"from={tomorrow}")).Total);

        var page = await ListAsync("pageSize=1&page=2");
        Assert.Equal([older.Id], page.Items.Select(o => o.Id));
        Assert.Equal(2, page.Total);
    }
}
