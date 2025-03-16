using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace PlanYonetimAraclari.Services
{
    public class TeamService : ITeamService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public TeamService(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<bool> InviteUserToProject(int projectId, string invitedEmail, string invitedByUserId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                    return false;

                var invitedByUser = await _context.Users.FindAsync(invitedByUserId);
                if (invitedByUser == null)
                    return false;

                // Check if user is already a member
                var existingMember = await _context.ProjectTeamMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.User.Email == invitedEmail);
                if (existingMember != null)
                    return false;

                // Check if there's already a pending invitation
                var existingInvitation = await _context.ProjectInvitations
                    .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.InvitedEmail == invitedEmail && i.Status == InvitationStatus.Pending);
                if (existingInvitation != null)
                    return false;

                var token = GenerateInvitationToken();
                var invitation = new ProjectInvitation
                {
                    ProjectId = projectId,
                    InvitedEmail = invitedEmail,
                    InvitedByUserId = invitedByUserId,
                    InvitedDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(2),
                    Status = InvitationStatus.Pending,
                    Token = token
                };

                _context.ProjectInvitations.Add(invitation);
                await _context.SaveChangesAsync();

                // Send invitation email
                var emailModel = new InvitationEmailModel
                {
                    LogoUrl = "https://plan345.com/images/logo.png",
                    ProjectName = project.Name,
                    InviterName = invitedByUser.FullName,
                    TeamMemberCount = project.TeamMembers?.Count ?? 0,
                    AcceptUrl = $"https://localhost:7066/Team/AcceptInvitation?token={token}",
                    DeclineUrl = $"https://localhost:7066/Team/DeclineInvitation?token={token}"
                };

                var emailSubject = "Plan345 - Proje Daveti";
                var emailBody = await RenderEmailTemplate("InvitationEmail", emailModel);

                await _emailService.SendEmailAsync(invitedEmail, emailSubject, emailBody);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> AcceptInvitation(string token)
        {
            var invitation = await _context.ProjectInvitations
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Token == token && i.Status == InvitationStatus.Pending);

            if (invitation == null || invitation.ExpiryDate < DateTime.UtcNow)
                return false;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == invitation.InvitedEmail);
            if (user == null)
                return false;

            var teamMember = new ProjectTeamMember
            {
                ProjectId = invitation.ProjectId,
                UserId = user.Id,
                Status = "Accepted",
                InvitedAt = invitation.InvitedDate,
                JoinedAt = DateTime.UtcNow
            };

            invitation.Status = InvitationStatus.Accepted;

            _context.ProjectTeamMembers.Add(teamMember);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeclineInvitation(string token)
        {
            var invitation = await _context.ProjectInvitations
                .FirstOrDefaultAsync(i => i.Token == token && i.Status == InvitationStatus.Pending);

            if (invitation == null)
                return false;

            invitation.Status = InvitationStatus.Declined;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CancelInvitation(int projectId, string userId)
        {
            var teamMember = await _context.ProjectTeamMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId && m.Status == "Pending");

            if (teamMember == null)
                return false;

            _context.ProjectTeamMembers.Remove(teamMember);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RemoveTeamMember(int projectId, string userId)
        {
            var teamMember = await _context.ProjectTeamMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);

            if (teamMember == null)
                return false;

            _context.ProjectTeamMembers.Remove(teamMember);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateTeamMemberRole(int projectId, string userId, TeamMemberRole newRole)
        {
            var teamMember = await _context.ProjectTeamMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);

            if (teamMember == null)
                return false;

            teamMember.Role = newRole;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<ProjectTeamMember>> GetProjectTeamMembers(int projectId)
        {
            return await _context.ProjectTeamMembers
                .Include(m => m.User)
                .Where(m => m.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<List<ProjectInvitation>> GetPendingInvitations(int projectId)
        {
            return await _context.ProjectInvitations
                .Include(i => i.InvitedByUser)
                .Where(i => i.ProjectId == projectId && i.Status == InvitationStatus.Pending)
                .ToListAsync();
        }

        public async Task<bool> IsUserProjectMember(int projectId, string userId)
        {
            return await _context.ProjectTeamMembers
                .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        }

        public async Task<TeamMemberRole?> GetUserProjectRole(int projectId, string userId)
        {
            var teamMember = await _context.ProjectTeamMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);

            return teamMember?.Role;
        }

        private string GenerateInvitationToken()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var tokenBytes = new byte[32];
                rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes)
                    .Replace("/", "_")
                    .Replace("+", "-")
                    .Replace("=", "");
            }
        }

        private async Task<string> RenderEmailTemplate(string templateName, object model)
        {
            // Bu metot e-posta şablonunu render edecek
            // Şimdilik basit bir şekilde InvitationEmail.cshtml içeriğini döndürüyoruz
            return await Task.FromResult($"Views/Emails/{templateName}.cshtml");
        }
    }
} 