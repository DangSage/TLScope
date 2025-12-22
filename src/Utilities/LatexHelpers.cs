namespace TLScope.Utilities;

/// <summary>
/// Utility methods for LaTeX string formatting and escaping
/// </summary>
public static class LatexHelpers
{
    /// <summary>
    /// Escape special LaTeX characters in text
    /// </summary>
    public static string EscapeLatex(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("\\", "\\textbackslash{}")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("$", "\\$")
            .Replace("&", "\\&")
            .Replace("%", "\\%")
            .Replace("#", "\\#")
            .Replace("_", "\\_")
            .Replace("~", "\\textasciitilde{}")
            .Replace("^", "\\textasciicircum{}");
    }

    /// <summary>
    /// Format bytes in human-readable format for LaTeX
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Format timestamp for LaTeX document
    /// </summary>
    public static string FormatTimestamp(DateTime dt)
    {
        return dt.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Format duration as human-readable string
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    /// <summary>
    /// Generate LaTeX color from RGB hex
    /// </summary>
    public static string RgbToLatexColor(string hexColor)
    {
        // Remove # if present
        hexColor = hexColor.TrimStart('#');

        if (hexColor.Length != 6)
            return "black";

        try
        {
            int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
            int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
            int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);

            // Convert to 0-1 range
            double rNorm = r / 255.0;
            double gNorm = g / 255.0;
            double bNorm = b / 255.0;

            return $"{{rgb,255:red,{r};green,{g};blue,{b}}}";
        }
        catch
        {
            return "black";
        }
    }

    /// <summary>
    /// Create a LaTeX table row
    /// </summary>
    public static string TableRow(params string[] cells)
    {
        return string.Join(" & ", cells.Select(EscapeLatex)) + " \\\\";
    }

    /// <summary>
    /// Shorten text to specified length with ellipsis
    /// </summary>
    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Format timestamp as compact relative time (e.g., "2m", "5h", "3d")
    /// </summary>
    public static string FormatRelativeTime(DateTime dt)
    {
        var elapsed = DateTime.UtcNow - dt;

        if (elapsed.TotalSeconds < 60)
            return $"{(int)elapsed.TotalSeconds}s";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h";
        if (elapsed.TotalDays < 30)
            return $"{(int)elapsed.TotalDays}d";
        if (elapsed.TotalDays < 365)
            return $"{(int)(elapsed.TotalDays / 30)}mo";
        return $"{(int)(elapsed.TotalDays / 365)}y";
    }
}
