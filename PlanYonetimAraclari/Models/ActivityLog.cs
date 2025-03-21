using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public ActivityType Type { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Title { get; set; }
        
        [StringLength(500)]
        public string Description { get; set; }
        
        public int? RelatedEntityId { get; set; }
        
        [StringLength(50)]
        public string RelatedEntityType { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
    
    public enum ActivityType
    {
        ProjectCreated,
        ProjectUpdated,
        ProjectDeleted,
        TaskCreated,
        TaskUpdated,
        TaskCompleted,
        TaskDeleted,
        NoteCreated,
        NoteUpdated,
        NoteDeleted,
        TeamMemberAdded,
        TeamMemberRemoved,
        CalendarNoteCreated,
        CalendarNoteUpdated,
        CalendarNoteDeleted
    }
} 