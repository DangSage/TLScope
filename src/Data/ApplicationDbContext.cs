// SQLite database context for the application.

using Microsoft.EntityFrameworkCore;
using TLScope.src.Models;
using TLScope.src.Debugging;

namespace TLScope.src.Data {
    /// <summary>
    /// Represents the database context for the application.
    /// </summary>
    /// <remarks>
    /// The database context is used to interact with the database and represents a session with the database.
    public class ApplicationDbContext : DbContext {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) {
            // log the information to the console
            if (Database.EnsureCreated()) {
                Logging.Write("Database instance created.");
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            //Ignore device properties from the TreeNode base class
            modelBuilder.Entity<Device>()
                .Ignore(d => d.Children)
                .Ignore(d => d.Text)
                .Ignore(d => d.Tag);

            // Configure entity relationships and constraints
            modelBuilder.Entity<User>()
                .HasMany(u => u.Devices)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId);
        }
    }
}
