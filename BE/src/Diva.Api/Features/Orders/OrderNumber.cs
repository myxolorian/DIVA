using Diva.Api.Features.Common;

namespace Diva.Api.Features.Orders;

/// <summary>Human-readable order numbers such as DIV-260930-0001.</summary>
public static class OrderNumber
{
    /// <summary>
    /// The date part is the WIB calendar date of the order. The running number comes from the
    /// PostgreSQL sequence order_number_seq: it never repeats and does not restart each day.
    /// </summary>
    public static string Format(DateTimeOffset createdAt, long sequence) =>
        $"DIV-{Wib.ToWib(createdAt):yyMMdd}-{sequence:D4}";
}
