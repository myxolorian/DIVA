using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diva.Api.Features.Receipts;

/// <summary>
/// The receipt as a PDF shaped like a till slip: 80 mm wide, as tall as the content.
/// QuestPDF builds the page from nested boxes (Column, Row, Item), a bit like HTML flexbox.
/// </summary>
public static class ReceiptPdf
{
    private const float FontSize = 8.5f;

    public static byte[] Create(ReceiptResponse r, string receiptUrl) =>
        Document.Create(document => document.Page(page =>
        {
            page.ContinuousSize(80, Unit.Millimetre);
            page.Margin(5, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(FontSize));

            page.Content().Column(col =>
            {
                col.Spacing(2);

                // Header: the laundry
                col.Item().AlignCenter().Text(r.Outlet.Name).Bold().FontSize(13);
                if (r.Outlet.Address is { } address)
                {
                    col.Item().AlignCenter().Text(address);
                }

                if (r.Outlet.Phone is { } phone)
                {
                    col.Item().AlignCenter().Text($"Telp. {phone}");
                }

                Divider(col);

                // Order and customer
                Pair(col, "No. Order", r.OrderNumber);
                Pair(col, "Tanggal", ReceiptFormat.DateTime(r.CreatedAt));
                if (r.DueDate is { } due)
                {
                    Pair(col, "Selesai", ReceiptFormat.Date(due));
                }

                Pair(col, "Customer", r.CustomerName);
                Pair(col, "Telp", r.CustomerPhone);

                Divider(col);

                // Lines: name on one row, "qty x price ... subtotal" on the next
                foreach (var item in r.Items)
                {
                    col.Item().Text(item.ServiceName).SemiBold();
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(
                            $"{ReceiptFormat.Quantity(item.Qty, item.BilledQty, item.Unit)} x {ReceiptFormat.Money(item.UnitPrice)}");
                        row.AutoItem().AlignRight().Text(ReceiptFormat.Money(item.Subtotal));
                    });
                }

                Divider(col);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                    row.AutoItem().AlignRight().Text(ReceiptFormat.Money(r.Total)).Bold().FontSize(11);
                });
                Pair(col, "Pembayaran", ReceiptFormat.Payment(r.PaymentStatus));
                Pair(col, "Status", r.Status.ToString());

                if (r.Notes is { } notes)
                {
                    Divider(col);
                    col.Item().Text($"Catatan: {notes}");
                }

                Divider(col);

                if (r.Outlet.Footer is { } footer)
                {
                    col.Item().AlignCenter().Text(footer).Italic();
                }

                col.Item().PaddingTop(4).AlignCenter().Text("Receipt online:").FontSize(7);
                col.Item().AlignCenter().Text(receiptUrl).FontSize(7).FontColor(Colors.Blue.Darken2);
            });
        })).GeneratePdf();

    private static void Pair(ColumnDescriptor col, string label, string value) =>
        col.Item().Row(row =>
        {
            row.ConstantItem(20, Unit.Millimetre).Text(label);
            row.RelativeItem().Text($": {value}");
        });

    private static void Divider(ColumnDescriptor col) =>
        col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
}
