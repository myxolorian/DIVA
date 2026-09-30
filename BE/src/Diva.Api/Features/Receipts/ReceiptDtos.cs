using Diva.Api.Domain;

namespace Diva.Api.Features.Receipts;

public sealed record ReceiptOutlet(string Name, string? Address, string? Phone, string? Footer);

public sealed record ReceiptItem(
    string ServiceName, ServiceUnit Unit, decimal Qty, decimal? MinQty, decimal BilledQty, decimal UnitPrice, decimal Subtotal);

/// <summary>
/// What a customer sees through the public link. Deliberately smaller than the staff view:
/// no customer id, no address, and a masked phone number.
/// </summary>
public sealed record ReceiptResponse(
    ReceiptOutlet Outlet,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    DateOnly? DueDate,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string CustomerName,
    string CustomerPhone,
    string? Notes,
    IReadOnlyList<ReceiptItem> Items,
    decimal Total,
    string PdfPath)
{
    public static ReceiptResponse From(Order order, OutletProfile outlet) => new(
        new ReceiptOutlet(outlet.Name, outlet.Address, outlet.Phone, outlet.ReceiptFooter),
        order.OrderNumber,
        order.CreatedAt,
        order.DueDate,
        order.Status,
        order.PaymentStatus,
        order.Customer!.Name,
        ReceiptFormat.MaskPhone(order.Customer.Phone),
        order.Notes,
        order.Items
            .OrderBy(i => i.LineNo)
            .Select(i => new ReceiptItem(i.ServiceName, i.Unit, i.Qty, i.MinQty, i.BilledQty, i.UnitPrice, i.Subtotal))
            .ToList(),
        order.Total,
        $"/api/public/receipts/{order.PublicToken}/pdf");
}
