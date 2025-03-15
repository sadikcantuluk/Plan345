using System.ComponentModel.DataAnnotations;

namespace PlanYonetimAraclari.Models
{
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Ad Soyad alanı zorunludur")]
        [Display(Name = "Ad Soyad")]
        public string Name { get; set; }
        
        [Required(ErrorMessage = "E-posta alanı zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        [Display(Name = "E-posta")]
        public string Email { get; set; }
        
        [Required(ErrorMessage = "Konu alanı zorunludur")]
        [Display(Name = "Konu")]
        public string Subject { get; set; }
        
        [Required(ErrorMessage = "Mesaj alanı zorunludur")]
        [Display(Name = "Mesaj")]
        public string Message { get; set; }
        
        [Required(ErrorMessage = "KVKK şartlarını kabul etmelisiniz")]
        [Display(Name = "KVKK şartlarını kabul ediyorum")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "KVKK şartlarını kabul etmelisiniz")]
        public bool AcceptPrivacy { get; set; }
    }
} 