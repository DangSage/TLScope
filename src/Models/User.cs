using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TLScope.src.Models {
    public class User {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

        [Required]
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

        [Required]
        [StringLength(50)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();

        // Device counter
        public int DeviceCount => Devices.Count;

        // * Temporary addition for testing
        //         public override string ToString()
        //         {
        //             return $@"
        // Id: {Id}
        // Username: {Username}
        // Password: {Password}
        // Email: {Email}
        // Role: {Role}
        // CreatedAt: {CreatedAt:yyyy-MM-dd HH:mm:ss}
        // UpdatedAt: {UpdatedAt:yyyy-MM-dd HH:mm:ss}
        // DeviceCount: {DeviceCount}
        // ";
        //         }
    }
}
