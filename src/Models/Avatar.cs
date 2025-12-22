namespace TLScope.Models;

/// <summary>
/// Represents an ASCII character avatar appearance
/// </summary>
public class Avatar
{
    /// <summary>
    /// ASCII art lines for the character (5 lines)
    /// </summary>
    public string[] Appearance { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Human-readable name (e.g., "normal", "angrier", "happy")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Enum identifier (e.g., "APPEARANCE_DEFAULT")
    /// </summary>
    public string Enum { get; set; } = string.Empty;

    /// <summary>
    /// Get the avatar as a multi-line string with color
    /// </summary>
    /// <param name="colorHex">Hex color code (e.g., "#FF5733")</param>
    /// <returns>Formatted avatar string</returns>
    public string GetColoredAvatar(string colorHex)
    {
        // Will be used with Terminal.Gui color attributes
        return string.Join(Environment.NewLine, Appearance);
    }

    /// <summary>
    /// Get avatar height in lines
    /// </summary>
    public int Height => Appearance.Length;

    /// <summary>
    /// Get avatar width (length of longest line)
    /// </summary>
    public int Width => Appearance.Max(line => line.Length);

    public override string ToString()
    {
        return Name;
    }
}
