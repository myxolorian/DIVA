using System.Net;
using System.Net.Http.Json;
using Diva.Api.Domain;
using Diva.Api.Features.Dashboard;
using Diva.Tests.Database;

namespace Diva.Tests.Features;

public class DashboardEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    // Only Summary_adds_up_every_number inserts orders; this class has its own empty database,
    // so all-time numbers (unpaid, status counts) are exact.
    private static readonly DateOnly Day = new(2020, 1, 15);

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) => new(2020, 1, day, hour, minute, 0, TimeSpan.Zero);

    private static Order NewOrder(
        Guid customerId, string number, DateTimeOffset createdAt, decimal total,
        OrderStatus status, PaymentStatus payment, DateOnly? due = null) => new()
    {
        OrderNumber = number,
        PublicToken = $"token{number}".PadRight(22, 'x'),
        CustomerId = customerId,
        CreatedAt = createdAt,
        Total = total,
        Status = status,
        PaymentStatus = payment,
        DueDate = due,
    };

    [PostgresFact]
    public async Task Requires_login()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task Summary_adds_up_every_number()
    {
        await using (var db = api.CreateDbContext())
        {
            var customer = new Customer { Name = "Ibu Sari", Phone = "081200000001" };
            db.Customers.Add(customer);
            db.Orders.AddRange(
                // 15 Jan WIB, paid, due today and still open
                NewOrder(customer.Id, "O1", Utc(15, 3), 50_000m, OrderStatus.Baru, PaymentStatus.Lunas, due: Day),
                // 15 Jan 23:00 WIB, unpaid, due yesterday and still open -> overdue
                NewOrder(customer.Id, "O2", Utc(15, 16), 30_000m, OrderStatus.Diproses, PaymentStatus.BelumLunas, due: Day.AddDays(-1)),
                // 14 Jan 17:30 UTC = 15 Jan 00:30 WIB -> counts for the 15th; past due but already finished
                NewOrder(customer.Id, "O3", Utc(14, 17, 30), 20_000m, OrderStatus.Selesai, PaymentStatus.BelumLunas, due: Day.AddDays(-2)),
                // 15 Jan 17:30 UTC = 16 Jan WIB -> tomorrow, outside the window
                NewOrder(customer.Id, "O4", Utc(15, 17, 30), 999m, OrderStatus.Diambil, PaymentStatus.Lunas),
                // 10 Jan WIB -> inside the 7-day window (9-15 Jan)
                NewOrder(customer.Id, "O5", Utc(10, 5), 40_000m, OrderStatus.Diambil, PaymentStatus.Lunas),
                // 8 Jan WIB -> before the window; unpaid; due in the future
                NewOrder(customer.Id, "O6", Utc(8, 5), 10_000m, OrderStatus.Baru, PaymentStatus.BelumLunas, due: Day.AddDays(5)));
            await db.SaveChangesAsync();
        }

        var summary = await api.CreateClient()
            .GetFromJsonAsync<DashboardSummary>($"/api/dashboard/summary?date={Day:yyyy-MM-dd}", TestJson.Options);

        Assert.Equal(Day, summary!.Date);
        Assert.Equal(new DayTotals(Orders: 3, Total: 100_000m, PaidTotal: 50_000m, UnpaidTotal: 50_000m), summary.Today);
        Assert.Equal(new UnpaidTotals(Orders: 3, Total: 60_000m), summary.Unpaid); // O2 + O3 + O6
        Assert.Equal(new StatusCounts(Baru: 2, Diproses: 1, Selesai: 1, Diambil: 2), summary.StatusCounts);
        Assert.Equal((1, 1), (summary.DueToday, summary.Overdue)); // O1 due today, O2 overdue

        Assert.Equal(
            [
                new DailyPoint(new DateOnly(2020, 1, 9), 0, 0m),
                new DailyPoint(new DateOnly(2020, 1, 10), 1, 40_000m),
                new DailyPoint(new DateOnly(2020, 1, 11), 0, 0m),
                new DailyPoint(new DateOnly(2020, 1, 12), 0, 0m),
                new DailyPoint(new DateOnly(2020, 1, 13), 0, 0m),
                new DailyPoint(new DateOnly(2020, 1, 14), 0, 0m),
                new DailyPoint(new DateOnly(2020, 1, 15), 3, 100_000m),
            ],
            summary.Last7Days);

        Assert.Equal(["O4", "O2", "O1", "O3", "O5"], summary.RecentOrders.Select(o => o.OrderNumber));
        Assert.Equal("Ibu Sari", summary.RecentOrders[0].CustomerName);
    }

    [PostgresFact]
    public async Task A_day_without_orders_has_zero_totals()
    {
        var summary = await api.CreateClient()
            .GetFromJsonAsync<DashboardSummary>("/api/dashboard/summary?date=2019-06-01", TestJson.Options);

        Assert.Equal(new DayTotals(0, 0m, 0m, 0m), summary!.Today);
        Assert.Equal(7, summary.Last7Days.Count);
        Assert.All(summary.Last7Days, d => Assert.Equal(0, d.Orders));
    }

    [PostgresFact]
    public async Task Without_a_date_it_summarizes_today()
    {
        var summary = await api.CreateClient().GetFromJsonAsync<DashboardSummary>("/api/dashboard/summary", TestJson.Options);

        Assert.Equal(Diva.Api.Features.Common.Wib.Today(TimeProvider.System), summary!.Date);
    }

    [PostgresFact]
    public async Task Invalid_date_is_rejected()
    {
        var response = await api.CreateClient().GetAsync("/api/dashboard/summary?date=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
