namespace Diva.Api.Features.Common;

/// <summary>Builds patterns for SQL LIKE/ILIKE.</summary>
public static class LikePattern
{
    /// <summary>
    /// "%term%", with the wildcard characters inside the term escaped. Without this, searching for
    /// "%" or "_" would match every row. Backslash is PostgreSQL's default LIKE escape character.
    /// </summary>
    public static string Contains(string term)
    {
        var escaped = term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        return $"%{escaped}%";
    }
}
