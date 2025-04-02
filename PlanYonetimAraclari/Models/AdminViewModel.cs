using System;
using System.Collections.Generic;

namespace PlanYonetimAraclari.Models
{
    public class AdminViewModel
    {
        public List<ApplicationUser> Users { get; set; }
        public int TotalUsers { get; set; }
        public int AdminUsers { get; set; }
        public int StandardUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int TotalProjects { get; set; }
        public double NewUserGrowth { get; set; }
        public double NewProjectGrowth { get; set; }
        
        // Kullanıcı detayları için model
        public class UserDetailsViewModel
        {
            public string Id { get; set; }
            public string UserName { get; set; }
            public string Email { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsEmailVerified { get; set; }
            public DateTime RegisterDate { get; set; }
            public DateTime? LastLoginDate { get; set; }
            public string ProfileImageUrl { get; set; }
            public int ProjectCount { get; set; }
            public int MaxProjectsAllowed { get; set; }
            public int MaxMembersPerProject { get; set; }
        }
    }
} 