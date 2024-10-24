using System;
using System.Linq;
using System.Diagnostics;

using TLScope.src.Models;
using TLScope.src.Utilities;
using TLScope.src.Data;
using TLScope.src.Debugging;

using Microsoft.EntityFrameworkCore;


namespace TLScope.src.Controllers {
    public class CLIController {
        private readonly string[] _args;
        private ApplicationDbContext? _dbContext;

        public CLIController(string[] args, ApplicationDbContext? dbContext) {
            _args = args ?? throw new ArgumentNullException(nameof(args));
            _dbContext = dbContext;
        }

        public void RunCLI() {
            if (_args.Length > 0) {
                switch (_args[0]) {
                    case "--version":
                        VersionInfo.TLScopeVersionCheck();
                        break;
                    case "--help":
                        DisplayHelp();
                        break;
                    case "--github":
                        OpenGitHubRepository();
                        break;
                    case "--register":
                        CreateAccount();
                        Console.WriteLine("Restart the application to log in.");
                        break;
                    default:
                        Console.WriteLine("Unknown option: " + _args[0]);
                        break;
                }
            } else {
                RunInteractiveMode();
            }
        }

        private void DisplayHelp() {
            Console.WriteLine("Usage: dotnet run [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --version    Display version information");
            Console.WriteLine("  --help       Display this help message");
            Console.WriteLine("  --github     Open the GitHub repository");
            Console.WriteLine("  --register   Register a new user");
        }

        private void OpenGitHubRepository() {
            Console.WriteLine("Opening the GitHub repository...");
            try {
                var psi = new ProcessStartInfo {
                    FileName = Constants.RepositoryUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);
            } catch (Exception ex) {
                Logging.Error($"Failed to open the GitHub repository", ex);
            }
        }

        private void CreateAccount() {
            Console.WriteLine("Registering a new user...");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Enter username: ");
            Console.ResetColor();
            string? username = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(username)) {
                Console.ResetColor();
                Console.WriteLine("Username cannot be empty.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Enter password: ");
            Console.ResetColor();
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

            _dbContext ??= new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite("Data Source=tlscope.db")
                .Options);
            _dbContext.Users.Add(user);
            _dbContext.SaveChanges();
            Console.ResetColor();

            Console.WriteLine("Account created successfully.");
            Logging.Write("Account created successfully. Located in the database @ " + user.Id);
        }

        private bool Login() {
            if (_dbContext == null) {
                Console.WriteLine("Database context is not initialized.");
                return false;
            }

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

        private void RunInteractiveMode() {
            Console.WriteLine("This not what you expected? Use --help for options.");
            // Initialize the dbContext if it is null
            if (_dbContext == null) {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite("Data Source=tlscope.db")
                    .Options;
                _dbContext = new ApplicationDbContext(options);
            }

            try {
                VersionInfo.TLScopeVersionCheck();
                bool userExists = _dbContext.Users.AsNoTracking().Any(); // Use no-tracking query
                if (!userExists) {
                    Console.WriteLine("No users found. Please create an account.");
                    CreateAccount();
                    Console.WriteLine("Please restart the application.");
                } else {
                    Console.WriteLine("Welcome back! Please log in.");
                    bool loginSuccessful = Login();
                    if (!loginSuccessful) {
                        return;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
