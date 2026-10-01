using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Diva.Api.Features.Orders;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public const int TrendDays = 7;
    public const int RecentCount = 5;

    // Orders that still need work: used for "due today" and "overdue".
    private static readonly OrderStatus[] OpenStatuses = [OrderStatus.Baru, OrderStatus.Diproses];

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/dashboard").WithTags("Dashboard").MapGet("/summary", Summary);
        return app;
    }

    /// <summary>
    /// Everything the home screen shows, in one request. Several small queries run one after
    /// another on the same connection (a DbContext cannot run two queries at the same time).
    /// </summary>
    private static async Task<IResult> Summary(DivaDbContext db, TimeProvider clock, DateOnly? date, CancellationToken ct)
    {
        var day = date ?? Wib.Today(clock);
        var orders = db.Orders.AsNoTracking();

        // 1. Orders created during the trend window (the last day of which is "today").
        //    Only CreatedAt, Total and PaymentStatus are loaded; they are grouped by WIB date in C#,
        //    which keeps the time zone rule in one place (Wib) instead of in SQL.
        var windowStart = Wib.StartOfDayUtc(day.AddDays(-(TrendDays - 1)));
        var windowEnd = Wib.StartOfDayUtc(day.AddDays(1));
        var recent = await orders
            .Where(o => o.CreatedAt >= windowStart && o.CreatedAt < windowEnd)
            .Select(o => new { o.CreatedAt, o.Total, o.PaymentStatus })
            .ToListAsync(ct);

        var byDay = recent.ToLookup(o => DateOnly.FromDateTime(Wib.ToWib(o.CreatedAt).DateTime));

        var todays = byDay[day].ToList();
        var today = new DayTotals(
            todays.Count,
            todays.Sum(o => o.Total),
            todays.Where(o => o.PaymentStatus == PaymentStatus.Lunas).Sum(o => o.Total),
            todays.Where(o => o.PaymentStatus != PaymentStatus.Lunas).Sum(o => o.Total));

        // Every day appears, also days without orders, so a chart has no gaps.
        var last7Days = Enumerable.Range(0, TrendDays)
            .Select(i => day.AddDays(i - (TrendDays - 1)))
            .Select(d => new DailyPoint(d, byDay[d].Count(), byDay[d].Sum(o => o.Total)))
            .ToList();

        // 2. Money that came in: counted on the moment an order was marked paid (PaidAt).
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var monthStartUtc = Wib.StartOfDayUtc(monthStart);
        var paid = await orders
            .Where(o => o.PaymentStatus == PaymentStatus.Lunas && o.PaidAt >= monthStartUtc && o.PaidAt < windowEnd)
            .Select(o => new { PaidAt = o.PaidAt!.Value, o.Total })
            .ToListAsync(ct);
        var paidToday = paid.Where(o => DateOnly.FromDateTime(Wib.ToWib(o.PaidAt).DateTime) == day).ToList();
        var income = new IncomeTotals(paidToday.Sum(o => o.Total), paidToday.Count, paid.Sum(o => o.Total), paid.Count);

        // 3. Money still to be collected, over all time: SQL COUNT and SUM.
        var unpaidQuery = orders.Where(o => o.PaymentStatus == PaymentStatus.BelumLunas);
        var unpaid = new UnpaidTotals(await unpaidQuery.CountAsync(ct), await unpaidQuery.SumAsync(o => o.Total, ct));

        // 4. How many orders are in each status: one GROUP BY query.
        var perStatus = await orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        int Count(OrderStatus status) => perStatus.GetValueOrDefault(status);
        var statusCounts = new StatusCounts(
            Count(OrderStatus.Baru), Count(OrderStatus.Diproses), Count(OrderStatus.Selesai), Count(OrderStatus.Diambil));

        // 5. Work that needs attention.
        var open = orders.Where(o => OpenStatuses.Contains(o.Status) && o.DueDate != null);
        var dueToday = await open.CountAsync(o => o.DueDate == day, ct);
        var overdue = await open.CountAsync(o => o.DueDate < day, ct);

        // 6. The newest orders, in the same shape as the order list.
        var recentOrders = await orders
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderNumber)
            .Take(RecentCount)
            .Select(OrderSummaryResponse.Projection)
            .ToListAsync(ct);

        return Results.Ok(new DashboardSummary(day, today, income, unpaid, statusCounts, dueToday, overdue, last7Days, recentOrders));
    }
}
