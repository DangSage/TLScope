using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TLScope.src.Models
{
    public class Device
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string DeviceName { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string IPAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(17)]
        public string MACAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Manufacturer { get; set; }

        [StringLength(100)]
        public string? Model { get; set; }

        [StringLength(100)]
        public string? OperatingSystem { get; set; }

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        // Foreign key to link to User
        public int UserId { get; set; }

        // Navigation property
        public virtual User? User { get; set; }

        // Navigation properties
        public virtual ICollection<NetworkInterface> NetworkInterfaces { get; set; } = new List<NetworkInterface>();

        //* Temporary addition for testing
        //         public override string ToString()
        //         {
        //             return $@"
        // Id: {Id}
        // DeviceName: {DeviceName}
        // IPAddress: {IPAddress}
        // MACAddress: {MACAddress}
        // Manufacturer: {Manufacturer}
        // Model: {Model}
        // OperatingSystem: {OperatingSystem}
        // LastSeen: {LastSeen:yyyy-MM-dd HH:mm:ss}
        // UserId: {UserId}
        // NetworkInterfaces: {NetworkInterfaces.Count}
        // User: {User?.Username}
        // ";
        //         }
    }

    public class NetworkInterface
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string InterfaceName { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string IPAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(17)]
        public string MACAddress { get; set; } = string.Empty;

        public int DeviceId { get; set; }

        // Navigation property
        public virtual Device? Device { get; set; }
    }
}