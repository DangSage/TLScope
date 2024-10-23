// Purpose: Contains utility classes for TLScope that don't fit into the specific scopes of the project.

using System;
using System.Reflection;
using System.Text;

namespace TLScope.src.Utilities {
    /// <summary>
    /// Provides version information about the application.
    /// </summary>
    /// <remarks>
    /// This class retrieves version information from the assembly attributes.
    /// </remarks>
    public static class VersionInfo {
        public static void TLScopeVersionCheck() {
            try {
                Console.WriteLine();
                var versionInfoBuilder = new StringBuilder();
                versionInfoBuilder.AppendLine("-*> TLScope <*-");
                versionInfoBuilder.AppendLine($"Version {GetVersion()}");
                versionInfoBuilder.AppendLine("Written by " + GetAuthor());
                versionInfoBuilder.AppendLine(Constants.RepositoryUrl);
                versionInfoBuilder.AppendLine(GetPackages());

                string versionInfo = versionInfoBuilder.ToString();
                string[] versioningInfoLines = versionInfo.Split('\n');

                int initialCursorLeft = Console.CursorLeft;
                int initialCursorTop = Console.CursorTop;

                foreach (string line in versioningInfoLines) {
                    ConsoleHelper.MoveCursorRelative(13, 0);
                    Console.WriteLine(line);
                    }

                int finalCursorTop = initialCursorTop + versioningInfoLines.Length;
                ConsoleHelper.MoveCursorRelative(0, -versioningInfoLines.Length);
                Console.Write(Constants.IconArt);
                ConsoleHelper.MoveCursorRelative(0, finalCursorTop - initialCursorTop - Constants.IconArt.Split('\n').Length);
                Console.WriteLine();
                ConsoleHelper.MoveCursorRelative(0, -1);
                } catch (Exception ex) {
                Console.WriteLine("An error occurred while checking the version of TLScope.");
                Console.WriteLine(ex.Message);
                }
            }

        public static string GetVersion() {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Version information not found";
            }

        public static string GetProductName() {
            var assembly = Assembly.GetExecutingAssembly();
            var productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
            return productAttribute?.Product ?? "Product name not found";
            }

        public static string GetAuthor() {
            var assembly = Assembly.GetExecutingAssembly();
            var companyAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            return companyAttribute?.Company ?? "Author information not found";
            }

        public static string GetConfiguration() {
            var assembly = Assembly.GetExecutingAssembly();
            var configurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            return configurationAttribute?.Configuration ?? "Configuration information not found";
            }

        public static string GetPackages() {
            // Get all the referenced assemblies
            var assemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
            var packagesBuilder = new StringBuilder();
            foreach (var assembly in assemblies) {
                if (assembly.Name != null && (assembly.Name.StartsWith("System.") || assembly.Name.StartsWith("System"))) {
                    continue;
                    }
                packagesBuilder.AppendLine($"Using {assembly.Name} version {assembly.Version}");
                }
            return packagesBuilder.ToString();
            }
        }

    /// <summary>
    /// Provides helper methods for the console for TLScopes uses.
    /// </summary>
    public static class ConsoleHelper {
        public static string ReadMaskedInput() {
            var passwordBuilder = new StringBuilder();
            ConsoleKeyInfo keyInfo;

            while (true) {
                keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter) {
                    Console.WriteLine();
                    break;
                    } else if (keyInfo.Key == ConsoleKey.Backspace) {
                    if (passwordBuilder.Length > 0) {
                        passwordBuilder.Length--;
                        Console.Write("\b \b");
                        }
                    } else if (!char.IsControl(keyInfo.KeyChar)) {
                    passwordBuilder.Append(keyInfo.KeyChar);
                    Console.Write("*");
                    }
                }

            return passwordBuilder.ToString();
            }

        public static void MoveCursorRelative(int columns, int rows) {
            int newLeft = Console.CursorLeft + columns;
            int newTop = Console.CursorTop + rows;

            // Ensure the new position is within the console window bounds
            newLeft = Math.Max(0, Math.Min(newLeft, Console.WindowWidth - 1));
            newTop = Math.Max(0, Math.Min(newTop, Console.WindowHeight - 1));

            Console.SetCursorPosition(newLeft, newTop);
            }
        }
    }
