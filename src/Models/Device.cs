using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Terminal.Gui.Trees;

namespace TLScope.src.Models {
    public class Device : ITreeNode {
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

        // ITreeNode implementation
        [NotMapped]
        public string Text {
            get => DeviceName;
            set => DeviceName = value ?? throw new ArgumentNullException(nameof(value));
        }

        [NotMapped]
        public object? Tag {
            get => this;
            set {}
        }

        public IList<ITreeNode> Children => new List<ITreeNode>();
    }
}
