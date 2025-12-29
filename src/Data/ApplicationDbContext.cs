using Microsoft.EntityFrameworkCore;
using TLScope.Models;

namespace TLScope.Data;

/// <summary>
/// Application database context for TLScope
/// </summary>
public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;
    public DbSet<DevicePort> DevicePorts { get; set; } = null!;
    public DbSet<TLSPeer> TLSPeers { get; set; } = null!;
    public DbSet<Connection> Connections { get; set; } = null!;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.SSHPublicKey).HasMaxLength(1000);
        });

        // Device configuration
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MACAddress).IsUnique();
            entity.HasIndex(e => e.IPAddress);
            entity.HasIndex(e => e.IsGateway);
            entity.HasIndex(e => e.IsTLScopePeer);
            entity.HasIndex(e => e.LastSeen);
            entity.Property(e => e.MACAddress).IsRequired().HasMaxLength(17);
            entity.Property(e => e.IPAddress).IsRequired().HasMaxLength(45); // IPv6 length
            entity.Property(e => e.Hostname).HasMaxLength(255);
            entity.Property(e => e.DeviceName).HasMaxLength(100);
            entity.Property(e => e.OperatingSystem).HasMaxLength(100);
            entity.Property(e => e.Vendor).HasMaxLength(100);

            // JSON conversion for OpenPorts list
            entity.Property(e => e.OpenPorts)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse).ToList()
                );

            // One-way relationship to TLSPeer (Device -> TLSPeer only)
            entity.HasOne(e => e.TLSPeer)
                .WithMany()
                .HasForeignKey(e => e.TLSPeerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Ports collection relationship
            entity.HasMany(e => e.Ports)
                .WithOne(p => p.Device)
                .HasForeignKey(p => p.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DevicePort configuration
        modelBuilder.Entity<DevicePort>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeviceId, e.Port, e.Protocol }).IsUnique();
            entity.HasIndex(e => e.Port);
            entity.Property(e => e.Protocol).IsRequired().HasMaxLength(10);
        });

        // TLSPeer configuration
        modelBuilder.Entity<TLSPeer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username);
            entity.HasIndex(e => e.IPAddress);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IPAddress).IsRequired().HasMaxLength(45);
            entity.Property(e => e.SSHPublicKey).HasMaxLength(1000);
            entity.Property(e => e.AvatarType).HasMaxLength(50);
            entity.Property(e => e.AvatarColor).HasMaxLength(7);
            entity.Property(e => e.Version).HasMaxLength(20);
            entity.Property(e => e.CombinedRandomartAvatar).HasMaxLength(600); // 11 lines × ~50 chars + separators
            entity.Property(e => e.IsVerified).IsRequired();
            entity.Property(e => e.LastVerified);
            entity.Ignore(e => e.IsConnected); // Transient property
        });

        // Connection configuration
        modelBuilder.Entity<Connection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SourceDeviceId, e.DestinationDeviceId, e.Protocol });
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.LastSeen);
            entity.Property(e => e.Protocol).IsRequired().HasMaxLength(10);
            entity.Property(e => e.State).HasMaxLength(20);

            // Relationships
            entity.HasOne(e => e.SourceDevice)
                .WithMany()
                .HasForeignKey(e => e.SourceDeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DestinationDevice)
                .WithMany()
                .HasForeignKey(e => e.DestinationDeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
