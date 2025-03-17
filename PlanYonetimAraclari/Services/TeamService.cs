using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        private readonly EmailSettings _emailSettings;

        public TeamService(ApplicationDbContext context, IEmailService emailService, IOptions<EmailSettings> emailSettings)
        {
            _context = context;
            _emailService = emailService;
            _emailSettings = emailSettings.Value;
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

                // Uygulama URL'sini yapılandırmadan alın veya varsayılan olarak localhost'u kullanın
                var baseUrl = _emailSettings.ApplicationUrl ?? "https://localhost:7091";

                // Send invitation email
                var emailModel = new InvitationEmailModel
                {
                    LogoUrl = "https://plan345.com/images/logo.png",
                    ProjectName = project.Name,
                    InviterName = invitedByUser.FullName,
                    TeamMemberCount = project.TeamMembers?.Count ?? 0,
                    AcceptUrl = $"{baseUrl}/Team/AcceptInvitation?token={token}",
                    DeclineUrl = $"{baseUrl}/Team/DeclineInvitation?token={token}"
                };

                var emailSubject = "Plan345 - Proje Daveti";
                var emailBody = await RenderEmailTemplate("InvitationEmail", emailModel);

                await _emailService.SendEmailAsync(invitedEmail, emailSubject, emailBody);

                return true;
            }
            catch (Exception ex)
            {
                // Hata yönetimi ekleyebilirsiniz
                System.Diagnostics.Debug.WriteLine($"Davet gönderme hatası: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AcceptInvitation(string token)
        {
            try
            {
                var invitation = await _context.ProjectInvitations
                    .Include(i => i.Project)
                    .FirstOrDefaultAsync(i => i.Token == token && i.Status == InvitationStatus.Pending);

                if (invitation == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Davet bulunamadı veya beklemede değil. Token: {token}");
                    return false;
                }

                if (invitation.ExpiryDate < DateTime.UtcNow)
                {
                    System.Diagnostics.Debug.WriteLine($"Davet süresi dolmuş. Son tarih: {invitation.ExpiryDate}, Şu an: {DateTime.UtcNow}");
                    return false;
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == invitation.InvitedEmail);
                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Kullanıcı bulunamadı. E-posta: {invitation.InvitedEmail}");
                    return false;
                }

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Davet kabul edilirken hata: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeclineInvitation(string token)
        {
            try
            {
                var invitation = await _context.ProjectInvitations
                    .FirstOrDefaultAsync(i => i.Token == token && i.Status == InvitationStatus.Pending);

                if (invitation == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Davet bulunamadı veya beklemede değil. Token: {token}");
                    return false;
                }

                invitation.Status = InvitationStatus.Declined;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Davet reddedilirken hata: {ex.Message}");
                return false;
            }
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
            // Davet e-posta şablonu için HTML içeriğini oluştur
            if (templateName == "InvitationEmail" && model is InvitationEmailModel inviteModel)
            {
                return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>Plan345 - Proje Daveti</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo {{
            max-width: 150px;
            height: auto;
        }}
        .content {{
            background-color: #f8f9fa;
            border-radius: 8px;
            padding: 30px;
            margin-bottom: 30px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #4f46e5;
            color: white !important;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 500;
            margin: 20px 0;
        }}
        .button:hover {{
            background-color: #4338ca;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #6b7280;
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #e5e7eb;
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <img src=""{inviteModel.LogoUrl}"" alt=""Plan345 Logo"" class=""logo"" />
    </div>

    <div class=""content"">
        <h1>Proje Daveti</h1>
        <p>Merhaba,</p>
        <p><strong>{inviteModel.InviterName}</strong> sizi <strong>{inviteModel.ProjectName}</strong> projesine davet ediyor.</p>
        
        <p>Proje hakkında:</p>
        <ul>
            <li>Proje: {inviteModel.ProjectName}</li>
            <li>Davet eden: {inviteModel.InviterName}</li>
            <li>Ekip üye sayısı: {inviteModel.TeamMemberCount}</li>
        </ul>

        <p>Daveti kabul etmek için aşağıdaki butona tıklayabilirsiniz:</p>
        
        <div style=""text-align: center;"">
            <a href=""{inviteModel.AcceptUrl}"" class=""button"" style=""background-color: #10b981; color: white !important;"">
                Daveti Kabul Et
            </a>
            <a href=""{inviteModel.DeclineUrl}"" class=""button"" style=""background-color: #ef4444; margin-left: 10px; color: white !important;"">
                Daveti Reddet
            </a>
        </div>

        <p style=""margin-top: 20px;"">
            <small>
                Bu davet 48 saat içinde geçerliliğini yitirecektir. Eğer butonlar çalışmıyorsa, 
                aşağıdaki bağlantıları tarayıcınıza kopyalayabilirsiniz:
            </small>
        </p>
        <p style=""word-break: break-all;"">
            <small>
                Kabul et: {inviteModel.AcceptUrl}<br />
                Reddet: {inviteModel.DeclineUrl}
            </small>
        </p>
    </div>

    <div class=""footer"">
        <p>Bu e-posta Plan345 tarafından gönderilmiştir.</p>
        <p>
            <small>
                Bu e-postayı yanlışlıkla aldıysanız lütfen dikkate almayınız ve siliniz. 
                Herhangi bir işlem yapmanıza gerek yoktur.
            </small>
        </p>
    </div>
</body>
</html>";
            }
            
            // Diğer e-posta şablonları için basit bir HTML oluştur
            return $@"<html><body><p>E-posta içeriği: {templateName}</p></body></html>";
        }
    }
} 