using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public class CalendarNote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        public DateTime NoteDate { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsCompleted { get; set; } = false;

        public bool NotificationSent { get; set; } = false;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
} 