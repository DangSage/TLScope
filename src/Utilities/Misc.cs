using System;
using System.Reflection;
using System.Text;

namespace TLScope.src.Utilities
{
    /// <summary>
    /// Provides version information about the application.
    /// </summary>
    /// <remarks>
    /// This class retrieves version information from the assembly attributes.
    /// </remarks>
    public static class VersionInfo
    {
        public static bool TLScopeVersionCheck()
        {
            try
            {
                Console.WriteLine();
                var versionInfoBuilder = new StringBuilder();
                versionInfoBuilder.AppendLine("-*> TLScope <*-");
                versionInfoBuilder.AppendLine(GetVersion());
                versionInfoBuilder.AppendLine("Written by " + GetAuthor());
                versionInfoBuilder.AppendLine(Constants.RepositoryUrl);
                versionInfoBuilder.AppendLine(GetPackages());

                string versionInfo = versionInfoBuilder.ToString();
                string[] versioningInfoLines = versionInfo.Split('\n');

                int initialCursorLeft = Console.CursorLeft;
                int initialCursorTop = Console.CursorTop;

                foreach (string line in versioningInfoLines)
                {
                    TLScopeMisc.MoveCursorRelative(13, 0);
                    Console.WriteLine(line);
                }

                int finalCursorTop = initialCursorTop + versioningInfoLines.Length;
                TLScopeMisc.MoveCursorRelative(0, -versioningInfoLines.Length);
                Console.Write(Constants.IconArt);
                TLScopeMisc.MoveCursorRelative(0, finalCursorTop - initialCursorTop - Constants.IconArt.Split('\n').Length);

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while checking the version of TLScope.");
                Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        public static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return versionAttribute?.InformationalVersion ?? "Version information not found";
        }

        public static string GetProductName()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
            return productAttribute?.Product ?? "Product name not found";
        }

        public static string GetAuthor()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var companyAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            return companyAttribute?.Company ?? "Author information not found";
        }

        public static string GetConfiguration()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var configurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            return configurationAttribute?.Configuration ?? "Configuration information not found";
        }

        public static string GetPackages()
        {
            // Get all the referenced assemblies
            var assemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
            var packagesBuilder = new StringBuilder();
            foreach (var assembly in assemblies)
            {
                if (assembly.Name != null && (assembly.Name.StartsWith("System.") || assembly.Name.StartsWith("System")))
                {
                    continue;
                }
                packagesBuilder.AppendLine($"Using {assembly.Name} version {assembly.Version}");
            }
            return packagesBuilder.ToString();
        }
    }

    public static class TLScopeMisc
    {
        public static void MoveCursorRelative(int columns, int rows)
        {
            int newLeft = Console.CursorLeft + columns;
            int newTop = Console.CursorTop + rows;

            // Ensure the new position is within the console window bounds
            newLeft = Math.Max(0, Math.Min(newLeft, Console.WindowWidth - 1));
            newTop = Math.Max(0, Math.Min(newTop, Console.WindowHeight - 1));

            Console.SetCursorPosition(newLeft, newTop);
        }
    }
}