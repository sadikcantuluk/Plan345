using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Services
{
    public interface IPlannerService
    {
        Task<List<PlannerTask>> GetTasksAsync(string userId);
        Task<PlannerTask> GetTaskByIdAsync(int taskId);
        Task SaveTaskAsync(PlannerTask task);
        Task UpdateTaskAsync(PlannerTask task);
        Task DeleteTaskAsync(int taskId);
        Task<List<PlannerTask>> GetChildTasksAsync(int parentTaskId);
        Task<List<PlannerTask>> GetRootTasksAsync(string userId);
        Task DeleteTaskWithChildrenAsync(int taskId);
        Task ReorderTasksAsync(int parentTaskId, List<int> taskIds);
        Task<List<PlannerTask>> GetTasksByProjectIdAsync(int projectId);
        
        // Diagnostik metodlar
        ApplicationDbContext GetDbContext();
        Task<int> GetTaskCountAsync();
    }
} 