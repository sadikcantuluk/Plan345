using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    public class TeamController : Controller
    {
        private readonly ITeamService _teamService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ApplicationDbContext _context;

        public TeamController(
            ITeamService teamService,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            ApplicationDbContext context)
        {
            _teamService = teamService;
            _userManager = userManager;
            _projectService = projectService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
            ViewData["UserEmail"] = user.Email;
            ViewData["UserProfileImage"] = user.ProfileImageUrl;
            ViewData["CurrentUserId"] = user.Id;

            // Get user's projects (both owned and team member) with User data included
            var projects = await _context.Projects
                .Include(p => p.User)
                .Include(p => p.TeamMembers)
                .Where(p => p.UserId == user.Id || p.TeamMembers.Any(m => m.UserId == user.Id))
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            return View(projects);
        }

        [HttpGet]
        public async Task<IActionResult> Members(int projectId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
                return NotFound();

            var isTeamMember = await _teamService.IsUserProjectMember(projectId, currentUser.Id);
            if (!isTeamMember && project.UserId != currentUser.Id)
                return Forbid();

            var teamMembers = await _teamService.GetProjectTeamMembers(projectId);
            var pendingInvitations = await _teamService.GetPendingInvitations(projectId);

            var viewModel = new TeamMembersViewModel
            {
                Project = project,
                TeamMembers = teamMembers,
                PendingInvitations = pendingInvitations,
                IsOwner = project.UserId == currentUser.Id
            };

            // Add user information to ViewData for _Layout.cshtml
            ViewData["UserFullName"] = $"{currentUser.FirstName} {currentUser.LastName}";
            ViewData["UserEmail"] = currentUser.Email;
            ViewData["UserProfileImage"] = currentUser.ProfileImageUrl;
            ViewData["CurrentUserId"] = currentUser.Id;

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> InviteMember(int projectId, string email)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
                return NotFound();

            if (project.UserId != currentUser.Id)
            {
                TempData["ErrorMessage"] = "Sadece proje sahibi yeni üye davet edebilir.";
                return RedirectToAction(nameof(Members), new { projectId });
            }

            var result = await _teamService.InviteUserToProject(projectId, email, currentUser.Id);
            if (result.success)
                TempData["SuccessMessage"] = "Davetiye başarıyla gönderildi.";
            else
                TempData["ErrorMessage"] = result.message;

            return RedirectToAction(nameof(Members), new { projectId });
        }

        [HttpGet]
        public async Task<IActionResult> RemoveMember(int projectId, string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
                return NotFound();

            if (project.UserId != currentUser.Id)
            {
                TempData["ErrorMessage"] = "Sadece proje sahibi üye çıkarabilir.";
                return RedirectToAction(nameof(Members), new { projectId });
            }

            if (project.UserId == userId)
            {
                TempData["ErrorMessage"] = "Proje sahibi ekipten çıkarılamaz.";
                return RedirectToAction(nameof(Members), new { projectId });
            }

            var result = await _teamService.RemoveTeamMember(projectId, userId);
            if (result)
                TempData["SuccessMessage"] = "Üye başarıyla çıkarıldı.";
            else
                TempData["ErrorMessage"] = "Üye çıkarılamadı. Lütfen tekrar deneyin.";

            return RedirectToAction(nameof(Members), new { projectId });
        }

        [HttpGet]
        public async Task<IActionResult> CancelInvitation(int projectId, int invitationId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
                return NotFound();

            if (project.UserId != currentUser.Id)
            {
                TempData["ErrorMessage"] = "Sadece proje sahibi daveti iptal edebilir.";
                return RedirectToAction(nameof(Members), new { projectId });
            }

            var invitation = await _context.ProjectInvitations
                .FirstOrDefaultAsync(i => i.Id == invitationId && i.ProjectId == projectId && i.Status == InvitationStatus.Pending);

            if (invitation == null)
            {
                TempData["ErrorMessage"] = "Davet bulunamadı veya zaten iptal edilmiş.";
                return RedirectToAction(nameof(Members), new { projectId });
            }

            invitation.Status = InvitationStatus.Cancelled;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Davet başarıyla iptal edildi.";
            return RedirectToAction(nameof(Members), new { projectId });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> AcceptInvitation(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Geçersiz davet tokenı");
                
            try
            {
                // Eğer kullanıcı giriş yapmamışsa, token bilgisini TempData'da saklayıp giriş sayfasına yönlendir
                if (!User.Identity.IsAuthenticated)
                {
                    TempData["InvitationToken"] = token;
                    TempData["InvitationAction"] = "Accept";
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("ProcessInvitation", "Team") });
                }

                var result = await _teamService.AcceptInvitation(token);
                if (result)
                    TempData["SuccessMessage"] = "Davet başarıyla kabul edildi.";
                else
                    TempData["ErrorMessage"] = "Davet kabul edilemedi. Davet geçersiz veya süresi dolmuş olabilir.";

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Davet işlenirken bir hata oluştu: {ex.Message}";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> DeclineInvitation(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Geçersiz davet tokenı");
                
            try
            {
                // Eğer kullanıcı giriş yapmamışsa, token bilgisini TempData'da saklayıp giriş sayfasına yönlendir
                if (!User.Identity.IsAuthenticated)
                {
                    TempData["InvitationToken"] = token;
                    TempData["InvitationAction"] = "Decline";
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("ProcessInvitation", "Team") });
                }

                var result = await _teamService.DeclineInvitation(token);
                if (result)
                    TempData["SuccessMessage"] = "Davet reddedildi.";
                else
                    TempData["ErrorMessage"] = "Davet reddedilemedi. Davet geçersiz veya süresi dolmuş olabilir.";

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Davet işlenirken bir hata oluştu: {ex.Message}";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ProcessInvitation()
        {
            try
            {
                // Giriş yapıldıktan sonra davet işleme
                var token = TempData["InvitationToken"] as string;
                var action = TempData["InvitationAction"] as string;

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(action))
                    return RedirectToAction("Index", "Dashboard");

                if (action == "Accept")
                {
                    var result = await _teamService.AcceptInvitation(token);
                    if (result)
                        TempData["SuccessMessage"] = "Davet başarıyla kabul edildi.";
                    else
                        TempData["ErrorMessage"] = "Davet kabul edilemedi. Davet geçersiz veya süresi dolmuş olabilir.";
                }
                else if (action == "Decline")
                {
                    var result = await _teamService.DeclineInvitation(token);
                    if (result)
                        TempData["SuccessMessage"] = "Davet reddedildi.";
                    else
                        TempData["ErrorMessage"] = "Davet reddedilemedi. Davet geçersiz veya süresi dolmuş olabilir.";
                }

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Davet işlenirken bir hata oluştu: {ex.Message}";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var pendingInvitations = await _context.ProjectInvitations
                .Include(i => i.Project)
                .Include(i => i.InvitedByUser)
                .Where(i => i.InvitedEmail == currentUser.Email && i.Status == InvitationStatus.Pending)
                .OrderByDescending(i => i.InvitedDate)
                .ToListAsync();

            var viewModel = new NotificationsViewModel
            {
                PendingInvitations = pendingInvitations
            };

            // Add user information to ViewData for _Layout.cshtml
            ViewData["UserFullName"] = $"{currentUser.FirstName} {currentUser.LastName}";
            ViewData["UserEmail"] = currentUser.Email;
            ViewData["UserProfileImage"] = currentUser.ProfileImageUrl;
            ViewData["CurrentUserId"] = currentUser.Id;

            return View(viewModel);
        }

        public async Task CheckPendingInvitations()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                var hasPendingInvitations = await _context.ProjectInvitations
                    .AnyAsync(i => i.InvitedEmail == currentUser.Email && i.Status == InvitationStatus.Pending);
                ViewData["HasPendingInvitations"] = hasPendingInvitations;
            }
        }

        [HttpPost]
        public async Task<IActionResult> LeaveProject(int projectId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return RedirectToAction("Login", "Account");

                var project = await _projectService.GetProjectByIdAsync(projectId);
                if (project == null)
                    return NotFound();

                // Proje sahibi projeden ayrılamaz
                if (project.UserId == currentUser.Id)
                {
                    return Json(new { success = false, message = "Proje sahibi projeden ayrılamaz" });
                }

                // Kullanıcının projede üye olup olmadığını kontrol et
                var isTeamMember = await _teamService.IsUserProjectMember(projectId, currentUser.Id);
                if (!isTeamMember)
                {
                    return Json(new { success = false, message = "Bu projenin bir üyesi değilsiniz" });
                }

                // Projeden ayrıl
                var result = await _teamService.RemoveTeamMember(projectId, currentUser.Id);
                if (result)
                {
                    return Json(new { success = true, message = "Projeden başarıyla ayrıldınız" });
                }
                else
                {
                    return Json(new { success = false, message = "Projeden ayrılırken bir hata oluştu" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }
    }
} 