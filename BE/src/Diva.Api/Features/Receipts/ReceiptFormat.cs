using System.Globalization;
using Diva.Api.Domain;
using Diva.Api.Features.Common;

namespace Diva.Api.Features.Receipts;

/// <summary>How values are written on a receipt (shared by the JSON and the PDF).</summary>
public static class ReceiptFormat
{
    public static string Money(decimal amount) => $"Rp{Rupiah.Number(amount)}";

    public static string Unit(ServiceUnit unit) => unit switch
    {
        ServiceUnit.Kg => "kg",
        ServiceUnit.Pcs => "pcs",
        ServiceUnit.M2 => "m²",
        _ => unit.ToString(),
    };

    /// <summary>"2 kg", or "2 kg (min. 3 kg)" when the minimum was charged instead.</summary>
    public static string Quantity(decimal qty, decimal billedQty, ServiceUnit unit)
    {
        var text = $"{Rupiah.Number(qty)} {Unit(unit)}";
        return billedQty > qty ? $"{text} (min. {Rupiah.Number(billedQty)} {Unit(unit)})" : text;
    }

    public static string DateTime(DateTimeOffset moment) =>
        Wib.ToWib(moment).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " WIB";

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Payment(PaymentStatus status) => status == PaymentStatus.Lunas ? "LUNAS" : "BELUM LUNAS";

    /// <summary>
    /// Anyone holding the link can read the receipt, so only part of the phone number is shown:
    /// 081234567890 → 0812*****890.
    /// </summary>
    public static string MaskPhone(string phone)
    {
        if (phone.Length <= 7)
        {
            return new string('*', phone.Length);
        }

        return phone[..4] + new string('*', phone.Length - 7) + phone[^3..];
    }
}
