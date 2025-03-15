using System;
using System.ComponentModel.DataAnnotations;

namespace PlanYonetimAraclari.Models
{
    public class TwoFactorCode
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public string Code { get; set; }
        
        [Required]
        public DateTime ExpiresAt { get; set; }
        
        [Required]
        public bool IsUsed { get; set; } = false;
    }
    
    public class TwoFactorCodeVerificationModel
    {
        [Required(ErrorMessage = "Doğrulama kodu gereklidir.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Doğrulama kodu 6 haneli olmalıdır.")]
        [Display(Name = "Doğrulama Kodu")]
        public string Code { get; set; }
        
        [Required]
        public string Email { get; set; }
    }
} 