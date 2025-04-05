using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public class PlannerTask
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public int Duration { get; set; } // Duration in days

        [Required(ErrorMessage = "UserId alanı gereklidir.")]
        public string UserId { get; set; }

        public int? ProjectId { get; set; }
        
        // Hiyerarşik yapı için gerekli alanlar
        public int? ParentTaskId { get; set; }
        
        public int OrderIndex { get; set; } // Görevi sıralamak için

        public DateTime CreatedAt { get; set; }
        
        public DateTime? UpdatedAt { get; set; }

        // Görev durumu (0: Bekliyor, 1: Devam Ediyor, 2: Tamamlandı, 3: İptal Edildi)
        public int TaskState { get; set; } = 0;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("ProjectId")]
        public virtual ProjectModel? Project { get; set; }
        
        [ForeignKey("ParentTaskId")]
        public virtual PlannerTask? ParentTask { get; set; }
        
        // Alt görevler koleksiyonu
        public virtual ICollection<PlannerTask> SubTasks { get; set; }
        
        public PlannerTask()
        {
            SubTasks = new HashSet<PlannerTask>();
            OrderIndex = 0;
        }
    }
} 