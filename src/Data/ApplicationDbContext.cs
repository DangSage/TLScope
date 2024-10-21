using Microsoft.EntityFrameworkCore;
using TLScope.src.Models;

namespace TLScope.src.Data
{
    /// <summary>
    /// Represents the database context for the application.
    /// </summary>
    /// <remarks>
    /// The database context is used to interact with the database and represents a session with the database.
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entity relationships and constraints
            modelBuilder.Entity<User>()
                .HasMany(u => u.Devices)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId);
        }
    }
}