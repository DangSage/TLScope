using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata.Ecma335;

using Terminal.Gui.Trees;

namespace TLScope.src.Models {
    public class Device : TreeNode {
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

        // Foreign key linking the Device to a User
        public int UserId { get; set; }
        public virtual User? User { get; set; }


        //* TreeNode properties and methods onwards

        // Constructor to initialize the TreeNode properties
        public Device() : base(string.Empty) {
            UpdateText();
        }

        // Method to update the Text property based on the DeviceName
        private void UpdateText() {
            Text = DeviceName;
        }

        // Override the Text property to keep it in sync with DeviceName
        [NotMapped]
        public override string Text {
            get => DeviceName ?? IPAddress;
            set => DeviceName = value ?? throw new ArgumentNullException(nameof(value));
        }

        [NotMapped]
        public new object? Tag;
    }
}
