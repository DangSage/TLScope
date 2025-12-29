using Microsoft.EntityFrameworkCore;
using TLScope.Data;
using TLScope.Models;
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
            CreatedAt = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        // Load SSH public key if path provided
        if (!string.IsNullOrEmpty(sshKeyPath) && File.Exists(sshKeyPath + ".pub"))
        {
            try
            {
                user.SSHPublicKey = await File.ReadAllTextAsync(sshKeyPath + ".pub");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load SSH public key");
            }
        }
        else
        {
            // Generate a temporary session SSH key fingerprint for SSH randomart
            user.SSHPublicKey = GenerateTemporarySSHKey(username);
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
    /// Generate a temporary SSH key fingerprint for SSH randomart generation
    /// </summary>
    private string GenerateTemporarySSHKey(string username)
    {
        // Generate a unique key based on username, session time, and random data
        var sessionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.Ticks;
        return $"ssh-temp {username}@{timestamp}-{sessionId.Substring(0, 8)} tlscope-session-key";
    }
}
