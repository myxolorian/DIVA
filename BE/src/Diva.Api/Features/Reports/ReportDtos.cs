using Diva.Api.Features.Dashboard;
using Diva.Api.Features.Orders;

namespace Diva.Api.Features.Reports;

/// <summary>
/// Money that came in between two WIB dates (both inclusive). An order counts on the day it was
/// marked paid, not the day it was created.
/// </summary>
public sealed record IncomeReport(
    DateOnly From,
    DateOnly To,
    decimal Total,
    int Orders,
    IReadOnlyList<DailyPoint> Days,
    IReadOnlyList<OrderSummaryResponse> Items);
