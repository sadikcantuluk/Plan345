using System.Collections.Generic;

namespace PlanYonetimAraclari.Models
{
    public class ProjectDetailViewModel
    {
        public ProjectModel Project { get; set; }
        
        public List<TaskModel> TodoTasks { get; set; } = new List<TaskModel>();
        public List<TaskModel> InProgressTasks { get; set; } = new List<TaskModel>();
        public List<TaskModel> DoneTasks { get; set; } = new List<TaskModel>();
        
        // Ekip üyeleri
        public List<ProjectTeamMemberViewModel> TeamMembers { get; set; } = new List<ProjectTeamMemberViewModel>();
        
        // Yeni görev oluşturmak için kullanılacak özellik
        public TaskModel NewTask { get; set; } = new TaskModel();
        
        // İstatistikler
        public int TotalTasksCount => TodoTasks.Count + InProgressTasks.Count + DoneTasks.Count;
        public int CompletionPercentage => TotalTasksCount > 0 ? (DoneTasks.Count * 100) / TotalTasksCount : 0;
    }
} 