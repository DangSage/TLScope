using System.Text.Json;

namespace TLScope.Models;

/// <summary>
/// Represents a local TLScope user
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password hash (Argon2)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// SSH private key path (for TLS peer authentication)
    /// </summary>
    public string? SSHPrivateKeyPath { get; set; }

    /// <summary>
    /// SSH public key (used to generate avatar color and authenticate)
    /// </summary>
    public string SSHPublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Avatar appearance enum from appearances.json
    /// </summary>
    public string AvatarType { get; set; } = "APPEARANCE_DEFAULT";

    /// <summary>
    /// Avatar color generated from SSH key hash (RGB hex format)
    /// </summary>
    public string AvatarColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Custom avatar lines (4 lines, 9 chars each)
    /// Stored in database as JSON array
    /// </summary>
    private string? _customAvatarLinesStorage;

    /// <summary>
    /// Custom avatar lines as array (overrides AvatarType if set)
    /// 4 lines of 9 characters each
    /// </summary>
    public string[]? CustomAvatarLines
    {
        get
        {
            if (string.IsNullOrEmpty(_customAvatarLinesStorage))
                return null;

            try
            {
                return JsonSerializer.Deserialize<string[]>(_customAvatarLinesStorage);
            }
            catch
            {
                return _customAvatarLinesStorage.Split('\n');
            }
        }
        set
        {
            if (value == null || value.Length == 0)
            {
                _customAvatarLinesStorage = null;
            }
            else
            {
                _customAvatarLinesStorage = JsonSerializer.Serialize(value);
            }
        }
    }

    /// <summary>
    /// Internal storage for custom avatar lines (used by EF Core)
    /// </summary>
    public string? CustomAvatarLinesStorage
    {
        get => _customAvatarLinesStorage;
        set => _customAvatarLinesStorage = value;
    }

    /// <summary>
    /// When the user account was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is this user currently active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    public override string ToString()
    {
        return $"{Username} ({Email})";
    }
}
