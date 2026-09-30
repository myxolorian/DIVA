namespace Diva.Api.Features.Common;

/// <summary>Error responses in the ProblemDetails format, with messages the frontend can show as-is.</summary>
public static class ApiResults
{
    public static IResult NotFound(string what) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"{what} tidak ditemukan.");

    public static IResult Conflict(string title, IDictionary<string, object?>? extensions = null) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title, extensions: extensions);
}
