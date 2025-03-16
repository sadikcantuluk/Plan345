using System.Collections.Generic;
using PlanYonetimAraclari.Models;

namespace PlanYonetimAraclari.Models
{
    public class NotificationsViewModel
    {
        public List<ProjectInvitation> PendingInvitations { get; set; }
        public string UserFullName { get; set; }
        public string UserEmail { get; set; }
        public string UserProfileImage { get; set; }
    }
} 