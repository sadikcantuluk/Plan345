using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;

namespace PlanYonetimAraclari.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [PersonalData]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [PersonalData]
        [StringLength(50)]
        public string LastName { get; set; }

        [PersonalData]
        public DateTime? LastLoginTime { get; set; }

        [PersonalData]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [PersonalData]
        public bool IsActive { get; set; } = true;

        [PersonalData]
        public string ProfileImageUrl { get; set; } = "/images/profiles/default.png";

        public string FullName => $"{FirstName} {LastName}";
    }
} 