using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TLScope.Utilities;

namespace TLScope.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext (used by EF Core migrations)
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var dbPath = ConfigurationHelper.GetConfigFilePath("tlscope.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
