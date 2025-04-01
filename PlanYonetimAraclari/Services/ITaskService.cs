using PlanYonetimAraclari.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskStatus = PlanYonetimAraclari.Models.TaskStatus;

namespace PlanYonetimAraclari.Services
{
    public interface ITaskService
    {
        Task<List<TaskModel>> GetUserAssignedTasksAsync(string userId, int count = 5);
        Task<List<TaskModel>> GetUserUpcomingTasksAsync(string userId, int daysAhead = 7, int count = 5);
        Task<bool> MarkTaskAsCompletedAsync(int taskId, string userId);
    }
} 