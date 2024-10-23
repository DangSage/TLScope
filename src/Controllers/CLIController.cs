using System;
using System.Linq;
using TLScope.src.Models;
using TLScope.src.Services;
using TLScope.src.Utilities;
using TLScope.src.Data;
using Microsoft.EntityFrameworkCore;
using TLScope.src.Debugging;
using System.Diagnostics;

namespace TLScope.src.Controllers {
    public class CLIController {
        private readonly string[] _args;
        private readonly ApplicationDbContext _dbContext;

        public CLIController(string[]? args, ApplicationDbContext? dbContext) {
            _args = args ?? throw new ArgumentNullException(nameof(args));

            // CLI options for running
            if (_args.Length > 0) {
                switch (_args[0]) {
                    case "--version":
                        VersionInfo.TLScopeVersionCheck();
                        Environment.Exit(0);
                        break;
                    case "--help":
                        VersionInfo.GetVersion();
                        Console.WriteLine("Usage: dotnet run [options]");
                        Console.WriteLine("Options:");
                        Console.WriteLine("  --version    Display version information");
                        Console.WriteLine("  --help       Display this help message");
                        Console.WriteLine("  --github     Open the GitHub repository in the default browser");
                        Environment.Exit(0);
                        break;
                    case "--github":
                        // Open the GitHub repository in the default browser
                        Console.WriteLine("Opening the GitHub repository...");
                        try {
                            var psi = new ProcessStartInfo {
                                FileName = Constants.RepositoryUrl,
                                UseShellExecute = true
                            };
                            Process.Start(psi);
                        } catch (Exception ex) {
                            Console.WriteLine($"Failed to open the GitHub repository: {ex.Message}");
                        }
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Unknown option: " + _args[0]);
                        Console.WriteLine("Use --help for more information.");
                        Environment.Exit(0);
                        break;
                }
            }

            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Runs the command-line interface (CLI) for the application.
        /// * Entry point for the application.
        /// </summary>
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
                    bool loginSuccessful = Login();
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
            string? password = ConsoleHelper.ReadMaskedInput();
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

        private bool Login() {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Enter username: ");
            Console.ResetColor();
            string? username = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(username)) {
                Console.ResetColor();
                Console.WriteLine("Username cannot be empty.");
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Enter password: ");
            Console.ResetColor();
            string? password = ConsoleHelper.ReadMaskedInput();
            if (string.IsNullOrWhiteSpace(password)) {
                Console.ResetColor();
                Console.WriteLine("Password cannot be empty.");
                return false;
            }

            var user = _dbContext.Users.SingleOrDefault(u => u.Username == username);
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
