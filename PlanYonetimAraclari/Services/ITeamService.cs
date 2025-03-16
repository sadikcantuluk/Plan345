using PlanYonetimAraclari.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PlanYonetimAraclari.Services
{
    public interface ITeamService
    {
        Task<bool> InviteUserToProject(int projectId, string invitedEmail, string invitedByUserId);
        Task<bool> AcceptInvitation(string token);
        Task<bool> DeclineInvitation(string token);
        Task<bool> RemoveTeamMember(int projectId, string userId);
        Task<bool> UpdateTeamMemberRole(int projectId, string userId, TeamMemberRole newRole);
        Task<List<ProjectTeamMember>> GetProjectTeamMembers(int projectId);
        Task<List<ProjectInvitation>> GetPendingInvitations(int projectId);
        Task<bool> IsUserProjectMember(int projectId, string userId);
        Task<TeamMemberRole?> GetUserProjectRole(int projectId, string userId);
        Task<bool> CancelInvitation(int projectId, string userId);
    }
} 