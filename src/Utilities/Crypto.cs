using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace TLScope.src.Utilities
{
    public static class Crypto
    {
        /// <summary>
        /// Creates a password hash using Argon2id.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <param name="passwordHash">The resulting password hash.</param>
        /// <param name="passwordSalt">The salt used in the hashing process.</param>
        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            // Generate a random salt
            passwordSalt = new byte[16];
            RandomNumberGenerator.Fill(passwordSalt);

            // Use Argon2id for password hashing
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                // Set the salt for the hashing process
                Salt = passwordSalt,
                // Set the degree of parallelism (number of threads to use)
                DegreeOfParallelism = 8,
                // Set the amount of memory to use (in KB)
                MemorySize = 65536,
                // Set the number of iterations
                Iterations = 4
            };

            // Generate a 32-byte hash
            passwordHash = argon2.GetBytes(32);
        }

        /// <summary>
        /// Verifies a password against a stored hash and salt using Argon2id.
        /// </summary>
        /// <param name="password">The password to verify.</param>
        /// <param name="storedHash">The stored password hash.</param>
        /// <param name="storedSalt">The stored salt used in the hashing process.</param>
        /// <returns>True if the password is correct, otherwise false.</returns>
        public static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            // Use Argon2id for password hashing
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                // Set the salt for the hashing process
                Salt = storedSalt,
                // Set the degree of parallelism (number of threads to use)
                DegreeOfParallelism = 8,
                // Set the amount of memory to use (in KB)
                MemorySize = 65536,
                // Set the number of iterations
                Iterations = 4
            };

            // Generate a 32-byte hash
            var computedHash = argon2.GetBytes(32);

            // Compare the computed hash with the stored hash
            for (int i = 0; i < storedHash.Length; i++)
            {
                if (computedHash[i] != storedHash[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}