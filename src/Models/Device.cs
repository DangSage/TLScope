using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TLScope.src.Models {
    public class Device {
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
        public string? OperatingSystem { get; set; }

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        // Foreign key to link to User
        public int UserId { get; set; }

        // Navigation property
        public virtual User? User { get; set; }

        // Navigation properties
        public virtual ICollection<ENetworkInterface> ENetworkInterfaces { get; set; } = new List<ENetworkInterface>();
    }

    public class ENetworkInterface {
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
