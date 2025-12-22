using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TLScope.Models;
using Spectre.Console;

namespace TLScope.Utilities;

/// <summary>
/// Utility for managing avatars and generating colors from SSH keys
/// </summary>
public static class AvatarUtility
{
    private static List<Avatar>? _avatars;

    /// <summary>
    /// Load all avatars from appearances.json
    /// </summary>
    public static List<Avatar> LoadAvatars(string appearancesJsonPath = "appearances.json")
    {
        if (_avatars != null)
            return _avatars;

        if (!File.Exists(appearancesJsonPath))
            throw new FileNotFoundException($"Appearances file not found: {appearancesJsonPath}");

        var json = File.ReadAllText(appearancesJsonPath);
        _avatars = JsonSerializer.Deserialize<List<Avatar>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<Avatar>();

        return _avatars;
    }

    /// <summary>
    /// Get avatar by enum name
    /// </summary>
    public static Avatar? GetAvatar(string enumName)
    {
        var avatars = _avatars ?? LoadAvatars();
        return avatars.FirstOrDefault(a => a.Enum == enumName);
    }

    /// <summary>
    /// Get random avatar
    /// </summary>
    public static Avatar GetRandomAvatar()
    {
        var avatars = _avatars ?? LoadAvatars();
        var random = new Random();
        return avatars[random.Next(avatars.Count)];
    }

    /// <summary>
    /// Generate RGB color from SSH public key using SHA256 hash
    /// </summary>
    /// <param name="sshPublicKey">SSH public key string</param>
    /// <returns>Hex color code (e.g., "#A3F2B7")</returns>
    public static string GenerateColorFromSSHKey(string sshPublicKey)
    {
        if (string.IsNullOrWhiteSpace(sshPublicKey))
            return "#FFFFFF"; // Default white

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sshPublicKey));

        var r = hash[0];
        var g = hash[1];
        var b = hash[2];

        r = (byte)Math.Max(128, (int)r);
        g = (byte)Math.Max(128, (int)g);
        b = (byte)Math.Max(128, (int)b);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Convert hex color to Spectre.Console RGB Color
    /// </summary>
    /// <param name="hexColor">Hex color code (e.g., "#A3F2B7")</param>
    /// <returns>RGB Color matching the hex value</returns>
    public static Color HexToSpectreColor(string hexColor)
    {
        hexColor = hexColor.TrimStart('#');

        if (hexColor.Length != 6)
            return Color.Default;

        var r = Convert.ToByte(hexColor.Substring(0, 2), 16);
        var g = Convert.ToByte(hexColor.Substring(2, 2), 16);
        var b = Convert.ToByte(hexColor.Substring(4, 2), 16);

        return new Color(r, g, b);
    }

    /// <summary>
    /// Get all available avatar names
    /// </summary>
    public static List<string> GetAvatarNames()
    {
        var avatars = _avatars ?? LoadAvatars();
        return avatars.Select(a => a.Name).ToList();
    }

    /// <summary>
    /// Get all available avatar enums
    /// </summary>
    public static List<string> GetAvatarEnums()
    {
        var avatars = _avatars ?? LoadAvatars();
        return avatars.Select(a => a.Enum).ToList();
    }
}
