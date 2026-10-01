using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Diva.Api.Features.Dashboard;
using Diva.Api.Features.Orders;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Reports;

public static class ReportEndpoints
{
    public const int MaxDays = 366;

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/reports").WithTags("Reports").MapGet("/income", Income);
        return app;
    }

    /// <summary>Income per day and the paid orders behind it. Default period: this month up to today.</summary>
    private static async Task<IResult> Income(
        DivaDbContext db, TimeProvider clock, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = Wib.Today(clock);
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);

        var errors = new ValidationErrors();
        if (start > end)
        {
            errors.Add("from", "Tanggal awal tidak boleh setelah tanggal akhir.");
        }
        else if (end.DayNumber - start.DayNumber + 1 > MaxDays)
        {
            errors.Add("from", $"Periode maksimal {MaxDays} hari.");
        }

        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        var startUtc = Wib.StartOfDayUtc(start);
        var endUtc = Wib.StartOfDayUtc(end.AddDays(1));
        var items = await db.Orders.AsNoTracking()
            .Where(o => o.PaymentStatus == PaymentStatus.Lunas && o.PaidAt >= startUtc && o.PaidAt < endUtc)
            .OrderByDescending(o => o.PaidAt).ThenByDescending(o => o.OrderNumber)
            .Select(OrderSummaryResponse.Projection)
            .ToListAsync(ct);

        // Grouped by WIB date in C#, like the dashboard, so the time zone rule stays in Wib.
        var byDay = items.ToLookup(o => DateOnly.FromDateTime(Wib.ToWib(o.PaidAt!.Value).DateTime));
        var days = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(i => start.AddDays(i))
            .Select(d => new DailyPoint(d, byDay[d].Count(), byDay[d].Sum(o => o.Total)))
            .ToList();

        return Results.Ok(new IncomeReport(start, end, items.Sum(o => o.Total), items.Count, days, items));
    }
}
