namespace TLScope.Utilities;

/// <summary>
/// ANSI escape codes for terminal colors and styles
/// These respect the terminal's color scheme
/// </summary>
public static class AnsiColors
{
    public const string Reset = "\x1b[0m";
    public const string Bold = "\x1b[1m";
    public const string Dim = "\x1b[2m";
    public const string Italic = "\x1b[3m";
    public const string Underline = "\x1b[4m";
    public const string Strikethrough = "\x1b[9m";

    public const string Black = "\x1b[30m";
    public const string Red = "\x1b[31m";
    public const string Green = "\x1b[32m";
    public const string Yellow = "\x1b[33m";
    public const string Blue = "\x1b[34m";
    public const string Magenta = "\x1b[35m";
    public const string Cyan = "\x1b[36m";
    public const string White = "\x1b[37m";
    public const string Gray = "\x1b[90m";
    public const string BrightRed = "\x1b[91m";
    public const string BrightGreen = "\x1b[92m";
    public const string BrightYellow = "\x1b[93m";
    public const string BrightBlue = "\x1b[94m";
    public const string BrightMagenta = "\x1b[95m";
    public const string BrightCyan = "\x1b[96m";
    public const string BrightWhite = "\x1b[97m";

    public const string BgBlack = "\x1b[40m";
    public const string BgRed = "\x1b[41m";
    public const string BgGreen = "\x1b[42m";
    public const string BgYellow = "\x1b[43m";
    public const string BgBlue = "\x1b[44m";
    public const string BgMagenta = "\x1b[45m";
    public const string BgCyan = "\x1b[46m";
    public const string BgWhite = "\x1b[47m";
    public const string BgGray = "\x1b[100m";

    /// <summary>
    /// Apply ANSI color to text
    /// </summary>
    public static string Colorize(string text, string color)
    {
        return $"{color}{text}{Reset}";
    }

    /// <summary>
    /// Apply multiple ANSI codes to text
    /// </summary>
    public static string Style(string text, params string[] codes)
    {
        var combined = string.Concat(codes);
        return $"{combined}{text}{Reset}";
    }
}
