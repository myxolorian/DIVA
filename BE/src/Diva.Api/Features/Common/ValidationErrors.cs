namespace Diva.Api.Features.Common;

/// <summary>
/// Collects validation messages per field and turns them into a standard 400 response:
/// { "title": "...", "status": 400, "errors": { "phone": ["..."] } }.
/// Field names are camelCase so they match the JSON the frontend sends.
/// </summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            _errors[field] = messages = [];
        }

        messages.Add(message);
    }

    /// <summary>Trims the value; returns null when it is missing or blank.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Records an error when a cleaned text value is longer than allowed.</summary>
    public void MaxLength(string field, string? value, int max)
    {
        if (value is not null && value.Length > max)
        {
            Add(field, $"Maksimal {max} karakter.");
        }
    }

    public IResult ToResult() => Results.ValidationProblem(
        _errors.ToDictionary(e => e.Key, e => e.Value.ToArray()),
        title: "Data yang dikirim tidak valid.");
}
