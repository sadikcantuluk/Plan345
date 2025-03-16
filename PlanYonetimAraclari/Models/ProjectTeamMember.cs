using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public class ProjectTeamMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public TeamMemberRole Role { get; set; }

        [Required]
        public string Status { get; set; } = "Pending";

        public DateTime InvitedAt { get; set; }

        public DateTime? JoinedAt { get; set; }

        [ForeignKey("ProjectId")]
        public virtual ProjectModel Project { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }

    public enum TeamMemberRole
    {
        [Display(Name = "Proje Sahibi")]
        Owner = 0,

        [Display(Name = "Yönetici")]
        Manager = 1,

        [Display(Name = "Üye")]
        Member = 2,

        [Display(Name = "Gözlemci")]
        Observer = 3
    }
} 