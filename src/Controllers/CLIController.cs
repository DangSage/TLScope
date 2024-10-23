using System;
using System.Linq;
using TLScope.src.Models;
using TLScope.src.Services;
using TLScope.src.Utilities;
using TLScope.src.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TLScope.src.Debugging;

namespace TLScope.src.Controllers {
    public class CLIController {
        private readonly ApplicationDbContext _dbContext;

        public CLIController(ApplicationDbContext dbContext) {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            Logging.Write("CLIController initialized.");

            // List all of the data in the database for debugging purposes
            foreach (var user in _dbContext.Users) {
                Logging.Write($"User: {user.Username}");
                }
            }

        /// <summary>
        /// Runs the command-line interface (CLI) for the application.
        /// </summary>
        /// <remarks>
        /// * This is the entry point for the application. (login or create account)
        public void RunCLI() {
            try {
                VersionInfo.TLScopeVersionCheck();
                bool userExists = _dbContext.Users.Any();
                if (!userExists) {
                    Console.WriteLine("No users found. Please create an account.");
                    CreateAccount();
                    Console.WriteLine("Please restart the application.");
                    } else {
                    Console.WriteLine("Welcome back! Please log in.\n");
                    bool loginSuccessful = Login(_dbContext);
                    if (!loginSuccessful) {
                        return;
                        }
                    }
                } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }

        private void CreateAccount() {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Enter username: ");
            string? username = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(username)) {
                Console.ResetColor();
                Console.WriteLine("Username cannot be empty.");
                return;
                }

            Console.Write("Enter password: ");
            string? password = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(password)) {
                Console.ResetColor();
                Console.WriteLine("Password cannot be empty.");
                return;
                }

            byte[] passwordHash, passwordSalt;
            Crypto.CreatePasswordHash(password, out passwordHash, out passwordSalt);

            var user = new User {
                Username = username,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
                };

            _dbContext.Users.Add(user);
            _dbContext.SaveChanges();
            Console.ResetColor();

            Console.WriteLine("Account created successfully.");
            Logging.Write("Account created successfully. Located in the database @ " + user.Id);
            }

        private static bool Login(ApplicationDbContext dbContext) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Enter username: ");
            string? username = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(username)) {
                Console.ResetColor();
                Console.WriteLine("Username cannot be empty.");
                return false;
                }

            Console.Write("Enter password: ");
            string? password = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(password)) {
                Console.ResetColor();
                Console.WriteLine("Password cannot be empty.");
                return false;
                }

            var user = dbContext.Users.SingleOrDefault(u => u.Username == username);
            if (user == null) {
                Console.ResetColor();
                Console.WriteLine("User not found.");
                return false;
                }

            bool isPasswordValid = Crypto.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt);
            if (!isPasswordValid) {
                Console.ResetColor();
                Console.WriteLine("Invalid Credentials.");
                return false;
                }

            Console.ResetColor();
            Console.WriteLine("Login successful.");
            return true;
            }
        }
    }
