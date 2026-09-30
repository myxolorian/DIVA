using System.Globalization;

namespace Diva.Api.Features.Common;

/// <summary>Indonesian number format for messages: 60.000 (not 60,000) and 2,5.</summary>
public static class Rupiah
{
    // Built by hand instead of CultureInfo("id-ID") so it works the same on every machine,
    // including servers that ship without culture data.
    private static readonly NumberFormatInfo Format = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
    };

    /// <summary>60000 → "60.000"; 7500.5 → "7.500,5".</summary>
    public static string Number(decimal value) =>
        value.ToString(decimal.Truncate(value) == value ? "N0" : "#,0.##", Format);
}
