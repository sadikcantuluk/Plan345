using System.ComponentModel.DataAnnotations;

namespace PlanYonetimAraclari.Models
{
    public class ProjectTeamMemberViewModel
    {
        public int MemberId { get; set; }
        public int ProjectId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserFullName { get; set; }
        public string UserProfileImage { get; set; }
        public TeamMemberRole Role { get; set; }
    }
} 