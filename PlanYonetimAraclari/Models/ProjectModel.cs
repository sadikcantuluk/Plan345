using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public class ProjectModel
    {
        [Key]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Proje adı zorunludur")]
        [StringLength(100, ErrorMessage = "Proje adı en fazla 100 karakter olabilir")]
        [Display(Name = "Proje Adı")]
        public string Name { get; set; }
        
        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir")]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }
        
        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedDate { get; set; }
        
        [Display(Name = "Son Güncelleme Tarihi")]
        public DateTime? LastUpdatedDate { get; set; }
        
        [Display(Name = "Bitiş Tarihi")]
        public DateTime? DueDate { get; set; }
        
        [Required]
        [Display(Name = "Durum")]
        public ProjectStatus Status { get; set; }
        
        [Display(Name = "Kullanıcı ID")]
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
    
    public enum ProjectStatus
    {
        [Display(Name = "Planlama")]
        Planning = 0,
        
        [Display(Name = "Devam Ediyor")]
        InProgress = 1,
        
        [Display(Name = "Tamamlandı")]
        Completed = 2,
        
        [Display(Name = "Beklemede")]
        OnHold = 3,
        
        [Display(Name = "İptal Edildi")]
        Cancelled = 4
    }
} 