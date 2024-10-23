using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using TLScope.src.Data;
using TLScope.src.Models;
using TLScope.src.Utilities; // Add this line to include the Crypto class

public class UserService {
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context) {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<User?> AuthenticateAsync(string username, string password) {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) {
            return null;
        }

        var user = await _context.Users.SingleOrDefaultAsync(x => x.Username == username);

        // Check if username exists
        if (user == null) {
            return null;
        }

        // Check if password is correct
        if (!Crypto.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt)) {
            return null;
        }

        // Authentication successful
        return user;
    }

    public async Task<IEnumerable<User>> GetAllAsync() {
        return await _context.Users.ToListAsync();
    }

    public async Task<User> GetByIdAsync(int id) {
        var user = await _context.Users.FindAsync(id);
        if (user == null) {
            throw new ArgumentNullException("User not found");
        }
        return user;
    }

    public async Task<User> CreateAsync(User user, string password) {
        // Validation
        if (string.IsNullOrWhiteSpace(password)) {
            throw new ArgumentNullException("Password is required");
        }

        if (_context.Users.Any(x => x.Username == user.Username)) {
            throw new ArgumentException("Username \"" + user.Username + "\" is already taken");
        }

        byte[] passwordHash, passwordSalt;
        Crypto.CreatePasswordHash(password, out passwordHash, out passwordSalt);

        user.PasswordHash = passwordHash;
        user.PasswordSalt = passwordSalt;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task UpdateAsync(User userParam, string? password = null) {
        var user = await _context.Users.FindAsync(userParam.Id);

        if (user == null) {
            throw new ArgumentNullException("User not found");
        }

        if (userParam.Username != user.Username) {
            // Username has changed so check if the new username is already taken
            if (_context.Users.Any(x => x.Username == userParam.Username)) {
                throw new ArgumentException("Username " + userParam.Username + " is already taken");
            }
        }

        // Update user properties
        user.Username = userParam.Username;

        // Update password if it was entered
        if (!string.IsNullOrWhiteSpace(password)) {
            Crypto.CreatePasswordHash(password, out var passwordHash, out var passwordSalt);
            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
        }

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}
