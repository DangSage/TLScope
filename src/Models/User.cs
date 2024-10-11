// module to define the User model in the database
// the User model class will be the backend data of the User table in the database

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TLScope.src.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string? Username { get; set; }

        [Required]
        [StringLength(50)]
        public string? Password { get; set; }

        [Required]
        [StringLength(50)]
        public string? Email { get; set; }

        [Required]
        [StringLength(50)]
        public string? Role { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // public List<Note>? Notes { get; set; }

        // when the object is printed, return all the properties of the object
        public override string ToString()
        {
            return $"Id: {Id}\nUsername: {Username}\nPassword: {Password}\nEmail: {Email}\nRole: {Role}\nCreatedAt: {CreatedAt}\nUpdatedAt: {UpdatedAt}";
        }
    }
}