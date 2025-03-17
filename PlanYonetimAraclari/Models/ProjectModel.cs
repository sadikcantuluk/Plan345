using System;
using System.Collections.Generic;
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
        
        [Required]
        [Display(Name = "Başlangıç Tarihi")]
        public DateTime StartDate { get; set; }
        
        [Required]
        [Display(Name = "Bitiş Tarihi")]
        public DateTime EndDate { get; set; }
        
        [Display(Name = "Bitiş Tarihi")]
        public DateTime? DueDate { get; set; }
        
        [Required]
        [Display(Name = "Durum")]
        public ProjectStatus Status { get; set; }
        
        [Required]
        [Display(Name = "Öncelik")]
        public ProjectPriority Priority { get; set; }
        
        [Display(Name = "Kullanıcı ID")]
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        // Team collaboration properties
        public virtual ICollection<ProjectTeamMember> TeamMembers { get; set; }
        public virtual ICollection<ProjectInvitation> Invitations { get; set; }

        public ProjectModel()
        {
            TeamMembers = new HashSet<ProjectTeamMember>();
            Invitations = new HashSet<ProjectInvitation>();
            CreatedDate = DateTime.Now;
            StartDate = DateTime.Now;
            EndDate = DateTime.Now.AddMonths(1);
            Status = ProjectStatus.Planning;
            Priority = ProjectPriority.Normal;
        }
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
    
    public enum ProjectPriority
    {
        [Display(Name = "Düşük")]
        Low = 0,
        
        [Display(Name = "Normal")]
        Normal = 1,
        
        [Display(Name = "Yüksek")]
        High = 2,
        
        [Display(Name = "Acil")]
        Urgent = 3
    }
} 