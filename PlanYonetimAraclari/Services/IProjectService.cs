using PlanYonetimAraclari.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskStatus = PlanYonetimAraclari.Models.TaskStatus;

namespace PlanYonetimAraclari.Services
{
    public interface IProjectService
    {
        Task<List<ProjectModel>> GetUserProjectsAsync(string userId);
        Task<ProjectModel> GetProjectByIdAsync(int projectId);
        Task<ProjectModel> CreateProjectAsync(ProjectModel project);
        Task<ProjectModel> UpdateProjectAsync(ProjectModel project);
        Task<bool> DeleteProjectAsync(int projectId, bool forceDelete = false);
        Task<int> GetUserProjectsCountAsync(string userId);
        Task<int> GetUserActiveProjectsCountAsync(string userId);
        Task<int> GetUserCompletedProjectsCountAsync(string userId);
        Task<int> GetUserPendingProjectsCountAsync(string userId);
        
        // Proje detayları ve görevleri için yeni metotlar
        Task<ProjectModel> GetProjectDetailsByIdAsync(int projectId);
        Task<bool> UpdateProjectStatusAsync(int projectId, ProjectStatus newStatus);
        Task<List<TaskModel>> GetProjectTasksAsync(int projectId);
        Task<List<TaskModel>> GetProjectTasksByStatusAsync(int projectId, TaskStatus status);
        Task<TaskModel> CreateTaskAsync(TaskModel task);
        Task<TaskModel> UpdateTaskAsync(TaskModel task);
        Task<bool> DeleteTaskAsync(int taskId);
        Task<int> GetUserOwnedProjectsCountAsync(string userId);
        Task<bool> IsUserProjectMemberAsync(string userId, int projectId);
    }
} 