using System.Collections.Generic;
using PlanYonetimAraclari.Models;

namespace PlanYonetimAraclari.Models
{
    public class TeamMembersViewModel
    {
        public ProjectModel Project { get; set; }
        public List<ProjectTeamMember> TeamMembers { get; set; }
        public List<ProjectInvitation> PendingInvitations { get; set; }
        public bool IsOwner { get; set; }
    }
} 