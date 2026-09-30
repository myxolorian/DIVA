using Diva.Api.Features.Orders;

namespace Diva.Api.Features.Dashboard;

/// <summary>Orders that came in on one day. "Total" is the value of those orders, paid or not.</summary>
public sealed record DayTotals(int Orders, decimal Total, decimal PaidTotal, decimal UnpaidTotal);

public sealed record UnpaidTotals(int Orders, decimal Total);

public sealed record StatusCounts(int Baru, int Diproses, int Selesai, int Diambil);

public sealed record DailyPoint(DateOnly Date, int Orders, decimal Total);

public sealed record DashboardSummary(
    DateOnly Date,
    DayTotals Today,
    UnpaidTotals Unpaid,
    StatusCounts StatusCounts,
    int DueToday,
    int Overdue,
    IReadOnlyList<DailyPoint> Last7Days,
    IReadOnlyList<OrderSummaryResponse> RecentOrders);
