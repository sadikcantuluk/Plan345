using System.Collections.Generic;
using PlanYonetimAraclari.Services;

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
        public List<ActivityLog> RecentActivities { get; set; } = new List<ActivityLog>();
        public ActivityService ActivityService { get; set; }
        
        // Upcoming Tasks & Reminders section
        public List<TaskModel> AssignedTasks { get; set; } = new List<TaskModel>();
        public List<CalendarNote> UpcomingCalendarNotes { get; set; } = new List<CalendarNote>();
    }
} 