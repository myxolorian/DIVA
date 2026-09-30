namespace Diva.Api.Features.Common;

/// <summary>
/// Waktu Indonesia Barat (UTC+7). The database stores UTC; dates the user sees (order number,
/// "today", date filters) are in WIB. Indonesia has no daylight saving, so a fixed offset is exact
/// and works the same on Windows and Linux.
/// </summary>
public static class Wib
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTimeOffset ToWib(DateTimeOffset moment) => moment.ToOffset(Offset);

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(ToWib(clock.GetUtcNow()).DateTime);

    /// <summary>The UTC moment at which the given WIB calendar day starts (00:00 WIB).</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();
}
