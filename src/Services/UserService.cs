using Microsoft.EntityFrameworkCore;
using TLScope.Data;
using TLScope.Models;
using TLScope.Utilities;
using Serilog;

namespace TLScope.Services;

/// <summary>
/// Service for user authentication and management
/// </summary>
public class UserService
{
    private readonly ApplicationDbContext _dbContext;

    public UserService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Authenticate or create a user
    /// </summary>
    public async Task<User?> AuthenticateOrCreateUser(string username, string? email = null, string? sshKeyPath = null)
    {
        // Try to find existing user
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user != null)
        {
            // Update last login
            user.LastLogin = DateTime.UtcNow;

            // Generate temporary SSH key if user doesn't have one
            if (string.IsNullOrEmpty(user.SSHPublicKey) || user.SSHPublicKey.StartsWith("default-"))
            {
                user.SSHPublicKey = GenerateTemporarySSHKey(username);
                user.AvatarColor = AvatarUtility.GenerateColorFromSSHKey(user.SSHPublicKey);
                Log.Information($"Generated temporary SSH key for existing user: {username}");
            }

            await _dbContext.SaveChangesAsync();
            Log.Information($"User {username} logged in");
            return user;
        }

        // Create new user
        user = new User
        {
            Username = username,
            Email = email ?? $"{username}@localhost",
            SSHPrivateKeyPath = sshKeyPath,
            AvatarType = "APPEARANCE_DEFAULT",
            CreatedAt = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        // Load SSH public key if path provided
        if (!string.IsNullOrEmpty(sshKeyPath) && File.Exists(sshKeyPath + ".pub"))
        {
            try
            {
                user.SSHPublicKey = await File.ReadAllTextAsync(sshKeyPath + ".pub");
                user.AvatarColor = AvatarUtility.GenerateColorFromSSHKey(user.SSHPublicKey);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load SSH public key");
            }
        }
        else
        {
            // Generate a temporary session SSH key fingerprint for unique color
            // This changes each session unless user defines a real SSH key
            user.SSHPublicKey = GenerateTemporarySSHKey(username);
            user.AvatarColor = AvatarUtility.GenerateColorFromSSHKey(user.SSHPublicKey);
            Log.Information($"Generated temporary SSH key for session: {username}");
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        Log.Information($"New user created: {username}");
        return user;
    }

    /// <summary>
    /// Get user by username
    /// </summary>
    public async Task<User?> GetUserByUsername(string username)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    /// <summary>
    /// Get all users
    /// </summary>
    public async Task<List<User>> GetAllUsers()
    {
        return await _dbContext.Users.ToListAsync();
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    public async Task<User?> UpdateUser(User user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
        Log.Information($"User {user.Username} updated");
        return user;
    }

    /// <summary>
    /// Save custom avatar lines for a user
    /// </summary>
    public async Task SaveCustomAvatar(User user, string[] lines)
    {
        if (lines.Length != 4)
        {
            throw new ArgumentException("Avatar must have exactly 4 lines", nameof(lines));
        }

        user.CustomAvatarLines = lines;

        await UpdateUser(user);
        Log.Information($"Custom avatar saved for user {user.Username}");
    }

    /// <summary>
    /// Get avatar lines for a user (custom or predefined)
    /// </summary>
    public string[] GetUserAvatar(User user)
    {
        // Check if user has custom avatar lines
        if (user.CustomAvatarLines != null && user.CustomAvatarLines.Length == 4)
        {
            return user.CustomAvatarLines;
        }

        // Fall back to predefined avatar
        var avatar = AvatarUtility.GetAvatar(user.AvatarType);
        return avatar?.Appearance ?? new[]
        {
            "   o   ",
            "./\\|/\\.",
            "( o.o )",
            " > ^ < "
        };
    }

    /// <summary>
    /// Clear custom avatar lines and revert to predefined avatar
    /// </summary>
    public async Task ClearCustomAvatar(User user)
    {
        user.CustomAvatarLines = null;

        await UpdateUser(user);
        Log.Information($"Custom avatar cleared for user {user.Username}");
    }

    /// <summary>
    /// Generate a temporary SSH key fingerprint for session-based unique colors
    /// </summary>
    private string GenerateTemporarySSHKey(string username)
    {
        // Generate a unique key based on username, session time, and random data
        var sessionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.Ticks;
        return $"ssh-temp {username}@{timestamp}-{sessionId.Substring(0, 8)} tlscope-session-key";
    }
}
