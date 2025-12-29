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
    /// SSH public key (used for SSH randomart generation and authentication)
    /// </summary>
    public string SSHPublicKey { get; set; } = string.Empty;

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
