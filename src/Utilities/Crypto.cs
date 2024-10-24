using System;
using System.Security.Cryptography;
using System.Text;

using Konscious.Security.Cryptography;

using TLScope.src.Debugging;

namespace TLScope.src.Utilities {
    public static class Crypto {
        /// <summary>
        /// Creates a password hash and salt using Argon2id.
        /// </summary>
        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt) {
            // Generate a random salt
            passwordSalt = new byte[16];
            RandomNumberGenerator.Fill(passwordSalt);

            // Use Argon2id for password hashing
            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))) {
                argon2.Salt = passwordSalt;
                argon2.DegreeOfParallelism = 8; // Number of threads to use
                argon2.MemorySize = 65536; // Amount of memory to use (in KB)
                argon2.Iterations = 4; // Number of iterations

                passwordHash = argon2.GetBytes(32); // Generate a 32-byte hash
            } // argon2.Dispose() is called automatically here

            Logging.Write($"Password hash: {BitConverter.ToString(passwordHash)}");
        }

        /// <summary>
        /// Verifies a password hash using Argon2id.
        /// </summary>
        public static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt) {
            byte[] computedHash;
            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))) {
                argon2.Salt = storedSalt;
                argon2.DegreeOfParallelism = 8;
                argon2.MemorySize = 65536;
                argon2.Iterations = 4;

                computedHash = argon2.GetBytes(32);
            } // argon2.Dispose() is called automatically here

            Logging.Write("Computed hash: " + BitConverter.ToString(computedHash));

            // Compare the computed hash with the stored hash using constant-time comparison
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
    }
}
