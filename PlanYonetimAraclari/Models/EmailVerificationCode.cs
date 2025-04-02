using System;
using System.ComponentModel.DataAnnotations;

namespace PlanYonetimAraclari.Models
{
    public class EmailVerificationCode
    {
        [Key]
        public int Id { get; set; }
        
        public string? UserId { get; set; }
        
        [Required]
        public string Email { get; set; }
        
        [Required]
        public string Code { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [Required]
        public DateTime ExpiresAt { get; set; }
        
        public bool IsUsed { get; set; } = false;
        
        // Number of verification attempts
        public int Attempts { get; set; } = 0;
    }
} 