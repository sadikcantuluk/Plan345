using PlanYonetimAraclari.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Services
{
    public interface IProjectService
    {
        Task<List<ProjectModel>> GetUserProjectsAsync(string userId);
        Task<ProjectModel> GetProjectByIdAsync(int projectId);
        Task<ProjectModel> CreateProjectAsync(ProjectModel project);
        Task<ProjectModel> UpdateProjectAsync(ProjectModel project);
        Task<bool> DeleteProjectAsync(int projectId);
        Task<int> GetUserProjectsCountAsync(string userId);
        Task<int> GetUserActiveProjectsCountAsync(string userId);
        Task<int> GetUserCompletedProjectsCountAsync(string userId);
        Task<int> GetUserPendingProjectsCountAsync(string userId);
    }
} 