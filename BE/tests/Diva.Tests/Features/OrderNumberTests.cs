using System.Text.RegularExpressions;
using Diva.Api.Features.Common;
using Diva.Api.Features.Orders;

namespace Diva.Tests.Features;

public class OrderNumberTests
{
    [Fact]
    public void Order_number_has_the_wib_date_and_a_padded_running_number()
    {
        var createdAt = new DateTimeOffset(2026, 9, 30, 3, 0, 0, TimeSpan.Zero);

        Assert.Equal("DIV-260930-0001", OrderNumber.Format(createdAt, 1));
        Assert.Equal("DIV-260930-12345", OrderNumber.Format(createdAt, 12_345));
    }

    [Fact]
    public void Date_part_follows_wib_not_utc()
    {
        // 17:30 UTC on 30 Sep is already 00:30 WIB on 1 Oct.
        var createdAt = new DateTimeOffset(2026, 9, 30, 17, 30, 0, TimeSpan.Zero);

        Assert.Equal("DIV-261001-0007", OrderNumber.Format(createdAt, 7));
    }

    [Fact]
    public void Wib_day_starts_at_17_00_utc_the_day_before()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero),
            Wib.StartOfDayUtc(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Public_tokens_are_22_url_safe_characters_and_do_not_repeat()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => PublicToken.Create()).ToList();

        Assert.All(tokens, t => Assert.Matches(new Regex("^[A-Za-z0-9_-]{22}$"), t));
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }
}
