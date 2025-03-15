using System.Collections.Generic;

namespace PlanYonetimAraclari.Models
{
    public class DashboardViewModel
    {
        public ProfileViewModel UserProfile { get; set; }
        public List<ProjectModel> Projects { get; set; }
        public int TotalProjectsCount { get; set; }
        public int ActiveProjectsCount { get; set; }
        public int CompletedProjectsCount { get; set; }
        public int PendingProjectsCount { get; set; }
    }
} 