using System.Text;

namespace FIS.Web.Helpers;

/// <summary>
/// Shared helper for building and parsing report filter URLs.
/// Supports the "result-view" pattern where Submit navigates to
/// the same route with ?view=report + serialized filter params.
/// </summary>
public static class ReportFilterHelper
{
    public const string ViewParamName = "view";
    public const string ViewModeResult = "report";

    /// <summary>Returns true when the current page is in result-view mode.</summary>
    public static bool IsResultView(string? viewParam)
        => string.Equals(viewParam, ViewModeResult, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds a result-view URL for the given base route with serialized filters.
    /// Non-null/non-empty filter values are appended as query parameters.
    /// </summary>
    public static string BuildResultUrl(string baseRoute, Dictionary<string, string?> filters)
    {
        var sb = new StringBuilder(baseRoute);
        sb.Append("?view=report");

        foreach (var (key, value) in filters)
        {
            if (!string.IsNullOrEmpty(value))
            {
                sb.Append('&');
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value));
            }
        }

        return sb.ToString();
    }

    /// <summary>Formats a nullable DateTime as ISO date string for query params.</summary>
    public static string? FormatDate(DateTime? date)
        => date?.ToString("yyyy-MM-dd");

    /// <summary>Parses an ISO date string from a query param. Returns null if invalid.</summary>
    public static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var d) ? d : null;

    /// <summary>Parses an int from a query param. Returns the default if invalid.</summary>
    public static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var n) ? n : defaultValue;
}
