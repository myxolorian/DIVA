using Diva.Api.Domain;
using Diva.Api.Features.Receipts;

namespace Diva.Tests.Features;

public class ReceiptFormatTests
{
    [Theory]
    [InlineData("081234567890", "0812*****890")]
    [InlineData("+6281234567890", "+628*******890")]
    [InlineData("12345678", "1234*678")]
    [InlineData("1234567", "*******")]
    public void Phone_numbers_are_partly_hidden(string phone, string expected)
    {
        Assert.Equal(expected, ReceiptFormat.MaskPhone(phone));
    }

    [Theory]
    [InlineData(2.0, 3.0, ServiceUnit.Kg, "2 kg (min. 3 kg)")]
    [InlineData(3.5, 3.5, ServiceUnit.Kg, "3,5 kg")]
    [InlineData(1.0, 1.0, ServiceUnit.Pcs, "1 pcs")]
    [InlineData(3.0, 4.0, ServiceUnit.M2, "3 m² (min. 4 m²)")]
    public void Quantity_mentions_the_minimum_when_it_was_charged(decimal qty, decimal billed, ServiceUnit unit, string expected)
    {
        Assert.Equal(expected, ReceiptFormat.Quantity(qty, billed, unit));
    }

    [Fact]
    public void Money_and_dates_use_indonesian_style()
    {
        Assert.Equal("Rp121.000", ReceiptFormat.Money(121_000m));
        // 17:30 UTC = 00:30 WIB the next day
        Assert.Equal("01/10/2026 00:30 WIB", ReceiptFormat.DateTime(new DateTimeOffset(2026, 9, 30, 17, 30, 0, TimeSpan.Zero)));
    }
}
