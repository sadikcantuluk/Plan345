using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanYonetimAraclari.Models
{
    public class ProjectInvitation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [EmailAddress]
        public string InvitedEmail { get; set; }

        [Required]
        public string InvitedByUserId { get; set; }

        [Required]
        public DateTime InvitedDate { get; set; } = DateTime.Now;

        public DateTime? ExpiryDate { get; set; } = DateTime.Now.AddDays(7);

        [Required]
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        public string? Token { get; set; }

        [ForeignKey("ProjectId")]
        public virtual ProjectModel Project { get; set; }

        [ForeignKey("InvitedByUserId")]
        public virtual ApplicationUser InvitedByUser { get; set; }
    }

    public enum InvitationStatus
    {
        [Display(Name = "Beklemede")]
        Pending = 0,

        [Display(Name = "Kabul Edildi")]
        Accepted = 1,

        [Display(Name = "Reddedildi")]
        Declined = 2,

        [Display(Name = "Süresi Doldu")]
        Expired = 3,

        [Display(Name = "İptal Edildi")]
        Cancelled = 4
    }
} 