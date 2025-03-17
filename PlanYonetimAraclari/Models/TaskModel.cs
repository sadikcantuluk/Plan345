using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public enum TaskStatus
    {
        [Display(Name = "Yapılacak")]
        Todo = 0,
        
        [Display(Name = "Devam Ediyor")]
        InProgress = 1,
        
        [Display(Name = "Tamamlandı")]
        Done = 2
    }
    
    public enum TaskPriority
    {
        [Display(Name = "Düşük")]
        Low = 0,
        
        [Display(Name = "Orta")]
        Medium = 1,
        
        [Display(Name = "Yüksek")]
        High = 2,
        
        [Display(Name = "Acil")]
        Urgent = 3
    }

    public class TaskModel
    {
        [Key]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Görev adı zorunludur")]
        [StringLength(100, ErrorMessage = "Görev adı en fazla 100 karakter olabilir")]
        [Display(Name = "Görev Adı")]
        public string Name { get; set; }
        
        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir")]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }
        
        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        [Display(Name = "Son Güncelleme Tarihi")]
        public DateTime? LastUpdatedDate { get; set; }
        
        [Display(Name = "Bitiş Tarihi")]
        public DateTime? DueDate { get; set; }
        
        [Required]
        [Display(Name = "Durum")]
        public TaskStatus Status { get; set; }
        
        [Display(Name = "Proje ID")]
        public int ProjectId { get; set; }
        
        [ForeignKey("ProjectId")]
        public virtual ProjectModel Project { get; set; }
        
        [Display(Name = "Önem Derecesi")]
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        
        [Display(Name = "Atanan Üye")]
        public string? AssignedMemberId { get; set; }
        
        [ForeignKey("AssignedMemberId")]
        public virtual ApplicationUser? AssignedMember { get; set; }
    }
} 