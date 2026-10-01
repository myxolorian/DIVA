using System.Net;
using System.Net.Http.Json;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Diva.Api.Features.Dashboard;
using Diva.Api.Features.Reports;
using Diva.Tests.Database;
using Microsoft.AspNetCore.Mvc;

namespace Diva.Tests.Features;

public class ReportEndpointTests(ApiWithDatabaseFixture api) : IClassFixture<ApiWithDatabaseFixture>
{
    private static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) =>
        new(2020, month, day, hour, minute, 0, TimeSpan.Zero);

    private static Order NewOrder(
        Guid customerId, string number, DateTimeOffset createdAt, decimal total,
        PaymentStatus payment, DateTimeOffset? paidAt) => new()
    {
        OrderNumber = number,
        PublicToken = $"token{number}".PadRight(22, 'x'),
        CustomerId = customerId,
        CreatedAt = createdAt,
        Total = total,
        PaymentStatus = payment,
        PaidAt = paidAt,
    };

    private async Task<ValidationProblemDetails> GetInvalidAsync(string query)
    {
        var response = await api.CreateClient().GetAsync($"/api/reports/income?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
    }

    [PostgresFact]
    public async Task Requires_login()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/api/reports/income");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task Income_is_counted_on_the_day_the_order_was_paid()
    {
        await using (var db = api.CreateDbContext())
        {
            var customer = new Customer { Name = "Pak Budi", Phone = "081300000001" };
            db.Customers.Add(customer);
            db.Orders.AddRange(
                // Ordered on 1 March, paid on 3 March -> income of 3 March
                NewOrder(customer.Id, "R1", Utc(3, 1, 3), 50_000m, PaymentStatus.Lunas, Utc(3, 3, 3)),
                // Paid 3 March 17:30 UTC = 4 March 00:30 WIB -> income of 4 March
                NewOrder(customer.Id, "R2", Utc(3, 3, 2), 20_000m, PaymentStatus.Lunas, Utc(3, 3, 17, 30)),
                // Not paid -> no income
                NewOrder(customer.Id, "R3", Utc(3, 3, 4), 99_000m, PaymentStatus.BelumLunas, null),
                // Paid before the period (29 Feb WIB)
                NewOrder(customer.Id, "R4", Utc(2, 28, 5), 7_000m, PaymentStatus.Lunas, Utc(2, 29, 5)),
                // Paid after the period (6 March WIB)
                NewOrder(customer.Id, "R5", Utc(3, 2, 5), 8_000m, PaymentStatus.Lunas, Utc(3, 6, 5)),
                // Set back to unpaid by hand (e.g. in the Supabase Table Editor) without clearing paid_at
                NewOrder(customer.Id, "R6", Utc(3, 2, 6), 6_000m, PaymentStatus.BelumLunas, Utc(3, 3, 6)));
            await db.SaveChangesAsync();
        }

        var report = await api.CreateClient()
            .GetFromJsonAsync<IncomeReport>("/api/reports/income?from=2020-03-01&to=2020-03-05", TestJson.Options);

        Assert.Equal((new DateOnly(2020, 3, 1), new DateOnly(2020, 3, 5)), (report!.From, report.To));
        Assert.Equal((70_000m, 2), (report.Total, report.Orders));
        Assert.Equal(
            [
                new DailyPoint(new DateOnly(2020, 3, 1), 0, 0m),
                new DailyPoint(new DateOnly(2020, 3, 2), 0, 0m),
                new DailyPoint(new DateOnly(2020, 3, 3), 1, 50_000m),
                new DailyPoint(new DateOnly(2020, 3, 4), 1, 20_000m),
                new DailyPoint(new DateOnly(2020, 3, 5), 0, 0m),
            ],
            report.Days);
        Assert.Equal(["R2", "R1"], report.Items.Select(o => o.OrderNumber)); // latest payment first
        Assert.Equal(Utc(3, 3, 17, 30), report.Items[0].PaidAt);
        Assert.Equal("Pak Budi", report.Items[0].CustomerName);
    }

    [PostgresFact]
    public async Task Without_dates_it_covers_this_month_up_to_today()
    {
        var today = Wib.Today(TimeProvider.System);

        var report = await api.CreateClient().GetFromJsonAsync<IncomeReport>("/api/reports/income", TestJson.Options);

        Assert.Equal((new DateOnly(today.Year, today.Month, 1), today), (report!.From, report.To));
        Assert.Equal(today.Day, report.Days.Count);
    }

    [PostgresFact]
    public async Task A_full_year_is_allowed()
    {
        var response = await api.CreateClient().GetAsync("/api/reports/income?from=2020-01-01&to=2020-12-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // 2020 is a leap year: 366 days
    }

    [PostgresFact]
    public async Task Invalid_periods_are_rejected()
    {
        var reversed = await GetInvalidAsync("from=2020-03-05&to=2020-03-01");
        var tooLong = await GetInvalidAsync("from=2020-01-01&to=2021-01-01");

        Assert.Contains("from", reversed.Errors.Keys);
        Assert.Contains("from", tooLong.Errors.Keys);
        var badDate = await api.CreateClient().GetAsync("/api/reports/income?from=abc");
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }
}
