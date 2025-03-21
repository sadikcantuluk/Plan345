using System.Collections.Generic;
using System.Linq;

namespace PlanYonetimAraclari.Models
{
    public class ReportsViewModel
    {
        public List<ProjectModel> Projects { get; set; } = new List<ProjectModel>();
        public List<TaskModel> Tasks { get; set; } = new List<TaskModel>();
        
        // İstatistik verileri
        public Dictionary<string, int> ProjectStats { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TaskStats { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> PriorityStats { get; set; } = new Dictionary<string, int>();
        
        // Son tamamlanan ve oluşturulan görevler
        public List<TaskModel> RecentCompletedTasks { get; set; } = new List<TaskModel>();
        public List<TaskModel> RecentCreatedTasks { get; set; } = new List<TaskModel>();
        
        // Özet verileri
        public int TotalProjects => Projects?.Count ?? 0;
        public int TotalTasks => Tasks?.Count ?? 0;
        public int CompletedTasks => Tasks?.Count(t => t.Status == PlanYonetimAraclari.Models.TaskStatus.Done) ?? 0;
        public int InProgressTasks => Tasks?.Count(t => t.Status == PlanYonetimAraclari.Models.TaskStatus.InProgress) ?? 0;
        public int PendingTasks => Tasks?.Count(t => t.Status == PlanYonetimAraclari.Models.TaskStatus.Todo) ?? 0;
        
        // Tamamlanma oranı
        public decimal TaskCompletionRate => TotalTasks > 0 ? (decimal)CompletedTasks / TotalTasks * 100 : 0;
        public decimal ProjectCompletionRate => TotalProjects > 0 ? (decimal)Projects.Count(p => p.Status == ProjectStatus.Completed) / TotalProjects * 100 : 0;
    }
} 