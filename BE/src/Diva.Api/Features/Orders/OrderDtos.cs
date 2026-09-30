using System.Linq.Expressions;
using Diva.Api.Domain;

namespace Diva.Api.Features.Orders;

public sealed record CreateOrderRequest(
    Guid? CustomerId,
    List<OrderItemRequest>? Items,
    string? Notes,
    DateOnly? DueDate,
    PaymentStatus? PaymentStatus);

/// <summary>One line of a new order. UnitPrice is only for services with a price range.</summary>
public sealed record OrderItemRequest(Guid? ServiceId, decimal? Qty, decimal? UnitPrice);

public sealed record UpdateStatusRequest(OrderStatus? Status);

public sealed record UpdatePaymentRequest(PaymentStatus? PaymentStatus);

public sealed record OrderCustomerResponse(Guid Id, string Name, string Phone, string? Address);

public sealed record OrderItemResponse(
    int LineNo,
    Guid ServiceId,
    string ServiceName,
    ServiceUnit Unit,
    decimal Qty,
    decimal? MinQty,
    decimal BilledQty,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    OrderCustomerResponse Customer,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    decimal Total,
    string? Notes,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    string PublicToken,
    string ReceiptPath,
    IReadOnlyList<OrderItemResponse> Items)
{
    /// <summary>Needs Customer and Items loaded.</summary>
    public static OrderResponse From(Order o) => new(
        o.Id,
        o.OrderNumber,
        new OrderCustomerResponse(o.Customer!.Id, o.Customer.Name, o.Customer.Phone, o.Customer.Address),
        o.Status,
        o.PaymentStatus,
        o.Total,
        o.Notes,
        o.DueDate,
        o.CreatedAt,
        o.PublicToken,
        $"/r/{o.PublicToken}",
        o.Items
            .OrderBy(i => i.LineNo)
            .Select(i => new OrderItemResponse(
                i.LineNo, i.ServiceId, i.ServiceName, i.Unit, i.Qty, i.MinQty, i.BilledQty, i.UnitPrice, i.Subtotal))
            .ToList());
}

/// <summary>One row in the order list.</summary>
public sealed record OrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    decimal Total,
    int ItemCount,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Written as an Expression (not a normal method) so EF Core can turn it into the SELECT
    /// columns of the SQL query. Shared by the order list and the dashboard.
    /// </summary>
    public static readonly Expression<Func<Order, OrderSummaryResponse>> Projection = o => new OrderSummaryResponse(
        o.Id,
        o.OrderNumber,
        o.CustomerId,
        o.Customer!.Name,
        o.Customer.Phone,
        o.Status,
        o.PaymentStatus,
        o.Total,
        o.Items.Count,
        o.DueDate,
        o.CreatedAt);
}
