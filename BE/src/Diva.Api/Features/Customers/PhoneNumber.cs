namespace Diva.Api.Features.Customers;

/// <summary>
/// Phone numbers are stored in one canonical form so that "0812-3456 7890" and "081234567890"
/// count as the same customer and can be found by the same search.
/// </summary>
public static class PhoneNumber
{
    public const int MinDigits = 8;
    public const int MaxDigits = 15; // E.164 upper limit

    /// <summary>Removes the separators people commonly type: spaces, '-', '.', '(' and ')'.</summary>
    public static string StripSeparators(string value) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '.' or '(' or ')')));

    /// <summary>Returns true for 8-15 digits, optionally prefixed with '+', once separators are removed.</summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var compact = StripSeparators(input);
        var hasPlus = compact.StartsWith('+');
        var digits = hasPlus ? compact[1..] : compact;

        if (digits.Length is < MinDigits or > MaxDigits || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        normalized = hasPlus ? "+" + digits : digits;
        return true;
    }
}
