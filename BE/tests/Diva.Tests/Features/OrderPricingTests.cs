using Diva.Api.Domain;
using Diva.Api.Features.Orders;

namespace Diva.Tests.Features;

public class OrderPricingTests
{
    private static LaundryService Service(decimal price, decimal? maxPrice = null, decimal? minQty = null) => new()
    {
        Name = "Tes",
        Category = "Tes",
        Unit = ServiceUnit.Kg,
        Price = price,
        MaxPrice = maxPrice,
        MinQty = minQty,
    };

    [Theory]
    [InlineData(2.0, 3.0, 3.0)] // below the minimum: charged as the minimum
    [InlineData(3.0, 3.0, 3.0)]
    [InlineData(3.5, 3.0, 3.5)] // above the minimum: charged as weighed
    [InlineData(0.5, null, 0.5)]
    public void Billed_quantity_is_raised_to_the_minimum(decimal qty, double? minQty, decimal expected)
    {
        Assert.Equal(expected, OrderPricing.BilledQty(qty, (decimal?)minQty));
    }

    [Fact]
    public void Examples_from_the_printed_price_list()
    {
        // 2 kg cuci kering setrika (min. 3 kg, 7.000/kg) and 3 m2 karpet tipis (min. 4 m2, 15.000/m2 = "MIN. RP 60.000").
        Assert.Equal(21_000m, OrderPricing.Subtotal(OrderPricing.BilledQty(2m, 3m), 7_000m));
        Assert.Equal(60_000m, OrderPricing.Subtotal(OrderPricing.BilledQty(3m, 4m), 15_000m));
    }

    [Fact]
    public void Subtotal_is_rounded_to_two_decimals_half_away_from_zero()
    {
        Assert.Equal(3.34m, OrderPricing.Subtotal(1.67m, 2m));
        Assert.Equal(0.01m, OrderPricing.Subtotal(0.01m, 0.5m)); // 0.005 rounds up, not to even
    }

    [Fact]
    public void Fixed_price_service_uses_its_own_price()
    {
        Assert.Null(OrderPricing.ResolveUnitPrice(Service(7_000m), null, out var price));
        Assert.Equal(7_000m, price);
    }

    [Fact]
    public void Fixed_price_service_refuses_a_price_from_the_request()
    {
        Assert.NotNull(OrderPricing.ResolveUnitPrice(Service(7_000m), 1m, out _));
    }

    [Theory]
    [InlineData(60_000)]
    [InlineData(100_000)]
    [InlineData(150_000)]
    public void Price_range_accepts_prices_within_the_range(decimal requested)
    {
        Assert.Null(OrderPricing.ResolveUnitPrice(Service(60_000m, maxPrice: 150_000m), requested, out var price));
        Assert.Equal(requested, price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(59_999.99)]
    [InlineData(150_000.01)]
    [InlineData(100_000.001)]
    public void Price_range_rejects_missing_or_out_of_range_prices(double? requested)
    {
        Assert.NotNull(OrderPricing.ResolveUnitPrice(Service(60_000m, maxPrice: 150_000m), (decimal?)requested, out _));
    }

    [Theory]
    [InlineData(2.5, ServiceUnit.Kg)]
    [InlineData(4.25, ServiceUnit.M2)]
    [InlineData(2, ServiceUnit.Pcs)]
    [InlineData(9999, ServiceUnit.Kg)]
    public void Valid_quantities(decimal qty, ServiceUnit unit)
    {
        Assert.Null(OrderPricing.CheckQty(qty, unit));
    }

    [Theory]
    [InlineData(null, ServiceUnit.Kg)]
    [InlineData(0.0, ServiceUnit.Kg)]
    [InlineData(-1.0, ServiceUnit.Kg)]
    [InlineData(10000.0, ServiceUnit.Kg)]
    [InlineData(1.234, ServiceUnit.Kg)]
    [InlineData(1.5, ServiceUnit.Pcs)] // half a jacket is not a thing
    public void Invalid_quantities(double? qty, ServiceUnit unit)
    {
        Assert.NotNull(OrderPricing.CheckQty((decimal?)qty, unit));
    }

    [Fact]
    public void Error_messages_use_indonesian_number_format()
    {
        var message = OrderPricing.ResolveUnitPrice(Service(60_000m, maxPrice: 150_000m), null, out _);

        Assert.Equal("Harga wajib diisi: antara 60.000 dan 150.000.", message);
    }

    [Theory]
    [InlineData(60000, "60.000")]
    [InlineData(100000000, "100.000.000")]
    [InlineData(7500.5, "7.500,5")]
    [InlineData(2.25, "2,25")]
    public void Rupiah_format(decimal value, string expected)
    {
        Assert.Equal(expected, Diva.Api.Features.Common.Rupiah.Number(value));
    }
}
