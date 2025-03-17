using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using PlanYonetimAraclari.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PlanYonetimAraclari.Hubs;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TaskHub> _taskHub;

        public ProjectController(
            ILogger<ProjectController> logger,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            ApplicationDbContext context,
            IHubContext<TaskHub> taskHub)
        {
            _logger = logger;
            _userManager = userManager;
            _projectService = projectService;
            _context = context;
            _taskHub = taskHub;
        }
        
        // Proje detayları sayfası
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return RedirectToAction("Login", "Account");

                var userEmail = await _userManager.GetEmailAsync(user);
                var project = await _projectService.GetProjectByIdAsync(id);

                if (project == null)
                    return NotFound();

                // Check if user has access to the project
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == id && m.UserId == user.Id);

                if (!isTeamMember && project.UserId != user.Id)
                    return Forbid();

                // Get user's role in the project
                bool isProjectOwner = project.UserId == user.Id;
                ViewBag.IsProjectOwner = isProjectOwner;
                ViewBag.IsTeamMember = isTeamMember;
                ViewBag.CanModifyTasks = isProjectOwner || isTeamMember; // Both owners and team members can modify tasks

                // StartDate'in değeri CreatedDate olmadıysa eşitle
                if (project.StartDate.Date != project.CreatedDate.Date)
                {
                    project.StartDate = project.CreatedDate;
                    await _projectService.UpdateProjectAsync(project);
                }

                List<TaskModel> todoTasks = null;
                List<TaskModel> inProgressTasks = null;
                List<TaskModel> doneTasks = null;

                try
                {
                    todoTasks = await _projectService.GetProjectTasksByStatusAsync(id, PlanYonetimAraclari.Models.TaskStatus.Todo);
                    inProgressTasks = await _projectService.GetProjectTasksByStatusAsync(id, PlanYonetimAraclari.Models.TaskStatus.InProgress);
                    doneTasks = await _projectService.GetProjectTasksByStatusAsync(id, PlanYonetimAraclari.Models.TaskStatus.Done);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Proje görevleri alınırken hata oluştu: {ex.Message}");
                    // Hata olsa bile boş listelerle devam ediyoruz, tamamen durdurmuyoruz
                }
                
                // View model oluştur
                var model = new ProjectDetailViewModel
                {
                    Project = project,
                    TodoTasks = todoTasks ?? new List<TaskModel>(),
                    InProgressTasks = inProgressTasks ?? new List<TaskModel>(),
                    DoneTasks = doneTasks ?? new List<TaskModel>(),
                    NewTask = new TaskModel { ProjectId = id, Status = PlanYonetimAraclari.Models.TaskStatus.Todo }
                };
                
                // Layout için gereken bilgileri ViewData'da sakla
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = userEmail;
                ViewData["UserProfileImage"] = user.ProfileImageUrl;
                ViewData["CurrentUserId"] = user.Id;
                
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje detayları görüntülenirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Proje detayları yüklenirken bir hata oluştu.";
                return RedirectToAction("Index", "Dashboard");
            }
        }
        
        // Proje durumu güncelleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ProjectStatus status)
        {
            try
            {
                // Session'dan kullanıcı bilgilerini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                
                // Kullanıcıyı bul
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    return Json(new { success = false, message = "Kullanıcı bilgileri bulunamadı." });
                }
                
                // Projeyi getir
                var project = await _projectService.GetProjectDetailsByIdAsync(id);
                if (project == null)
                {
                    _logger.LogWarning($"Proje bulunamadı: {id}");
                    return Json(new { success = false, message = "Proje bulunamadı." });
                }
                
                // Projenin sahibi mi kontrol et
                if (project.UserId != user.Id)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının ({project.UserId}) projesini güncellemeye çalışıyor.");
                    return Json(new { success = false, message = "Bu projeyi güncelleme yetkiniz yok." });
                }
                
                // Proje durumunu güncelle
                var result = await _projectService.UpdateProjectStatusAsync(id, status);
                if (result)
                {
                    return Json(new { success = true, message = "Proje durumu başarıyla güncellendi." });
                }
                else
                {
                    return Json(new { success = false, message = "Proje durumu güncellenirken bir hata oluştu." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje durumu güncellenirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = $"Proje durumu güncellenirken bir hata oluştu: {ex.Message}" });
            }
        }
        
        // Proje silme 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return RedirectToAction("Login", "Account");

                // Layout için gereken bilgileri ViewData'da sakla
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = user.Email;
                ViewData["UserProfileImage"] = user.ProfileImageUrl;
                ViewData["CurrentUserId"] = user.Id;

                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                    return NotFound();

                // Only project owner can delete the project
                if (project.UserId != user.Id)
                {
                    TempData["ErrorMessage"] = "Bu projeyi silme yetkiniz yok.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Projeyi silerken güvenlik amacıyla forceDelete parametresini true yapalım
                var result = await _projectService.DeleteProjectAsync(id, true);
                if (result)
                {
                    TempData["SuccessMessage"] = "Proje başarıyla silindi.";
                    return RedirectToAction("Index", "Dashboard");
                }

                TempData["ErrorMessage"] = "Proje silinirken bir hata oluştu.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje silinirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = $"Proje silinirken bir hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        
        // Görev oluşturma
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(TaskModel model)
        {
            try
            {
                // Session'dan kullanıcı bilgilerini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                
                // Kullanıcıyı bul
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    TempData["ErrorMessage"] = "Kullanıcı bilgileri bulunamadı.";
                    return RedirectToAction("Details", new { id = model.ProjectId });
                }
                
                // Projeyi getir
                var project = await _projectService.GetProjectDetailsByIdAsync(model.ProjectId);
                if (project == null)
                {
                    _logger.LogWarning($"Proje bulunamadı: {model.ProjectId}");
                    TempData["ErrorMessage"] = "Proje bulunamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Projenin sahibi veya ekip üyesi olup olmadığını kontrol et
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == model.ProjectId && m.UserId == user.Id);
                
                if (project.UserId != user.Id && !isTeamMember)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) projeye görev ekleme yetkisine sahip değil.");
                    TempData["ErrorMessage"] = "Bu projeye görev ekleme yetkiniz yok.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Manuel doğrulama
                if (string.IsNullOrEmpty(model.Name))
                {
                    TempData["ErrorMessage"] = "Görev adı zorunludur.";
                    return RedirectToAction("Details", new { id = model.ProjectId });
                }
                
                // Görev oluştur
                var task = await _projectService.CreateTaskAsync(model);
                
                // SignalR ile yeni görevi tüm bağlı kullanıcılara bildir
                await _taskHub.Clients.Group($"project_{model.ProjectId}").SendAsync("ReceiveNewTask", task);
                
                TempData["SuccessMessage"] = "Görev başarıyla oluşturuldu.";
                return RedirectToAction("Details", new { id = model.ProjectId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev oluşturulurken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = $"Görev oluşturulurken bir hata oluştu: {ex.Message}";
                return RedirectToAction("Details", new { id = model.ProjectId });
            }
        }
        
        // Görev durumu güncelleme (AJAX)
        [HttpPost]
        public async Task<IActionResult> UpdateTaskStatus(int taskId, PlanYonetimAraclari.Models.TaskStatus newStatus)
        {
            try
            {
                // Session'dan kullanıcı bilgilerini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                
                // Kullanıcıyı bul
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    return Json(new { success = false, message = "Kullanıcı bilgileri bulunamadı." });
                }
                
                // Görevi doğrudan veritabanından ID'ye göre getir
                var task = await _context.Tasks.FindAsync(taskId);
                
                if (task == null)
                {
                    _logger.LogWarning($"Görev bulunamadı: {taskId}");
                    return Json(new { success = false, message = "Görev bulunamadı." });
                }
                
                // Proje sahibi veya ekip üyesi kontrolü
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == user.Id);
                    
                if (!isTeamMember && !(await _projectService.GetProjectDetailsByIdAsync(task.ProjectId)).UserId.Equals(user.Id))
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) görevi güncelleme yetkisine sahip değil.");
                    return Json(new { success = false, message = "Bu görevi güncelleme yetkiniz yok." });
                }
                
                // Görev durumunu güncelle
                task.Status = newStatus;
                task.LastUpdatedDate = DateTime.Now;
                
                var updatedTask = await _projectService.UpdateTaskAsync(task);
                
                // SignalR ile görev durumu değişikliğini tüm bağlı kullanıcılara bildir
                await _taskHub.Clients.Group($"project_{task.ProjectId}").SendAsync("ReceiveTaskStatusChange", taskId, newStatus);
                
                return Json(new { 
                    success = true, 
                    message = "Görev durumu başarıyla güncellendi.",
                    task = new {
                        id = updatedTask.Id,
                        name = updatedTask.Name,
                        description = updatedTask.Description,
                        status = updatedTask.Status,
                        priority = updatedTask.Priority,
                        dueDate = updatedTask.DueDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev durumu güncellenirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = $"Görev durumu güncellenirken bir hata oluştu: {ex.Message}" });
            }
        }
        
        // Görev silme (AJAX)
        [HttpPost]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            try
            {
                // Session'dan kullanıcı bilgilerini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                
                // Kullanıcıyı bul
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    return Json(new { success = false, message = "Kullanıcı bilgileri bulunamadı." });
                }
                
                // Görevi doğrudan veritabanından ID'ye göre getir
                var task = await _context.Tasks.FindAsync(taskId);
                
                if (task == null)
                {
                    _logger.LogWarning($"Görev bulunamadı: {taskId}");
                    return Json(new { success = false, message = "Görev bulunamadı." });
                }
                
                int projectId = task.ProjectId; // SignalR bildiriminde kullanmak için projeId'yi sakla
                
                // Proje sahibi veya ekip üyesi kontrolü
                var project = await _projectService.GetProjectDetailsByIdAsync(task.ProjectId);
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == user.Id);
                
                if (project.UserId != user.Id && !isTeamMember)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının projesindeki görevi silmeye çalışıyor.");
                    return Json(new { success = false, message = "Bu görevi silme yetkiniz yok." });
                }
                
                // Görevi sil
                var result = await _projectService.DeleteTaskAsync(taskId);
                
                // SignalR ile görev silme işlemini tüm bağlı kullanıcılara bildir
                await _taskHub.Clients.Group($"project_{projectId}").SendAsync("ReceiveTaskDelete", taskId);
                
                return Json(new { 
                    success = true, 
                    message = "Görev başarıyla silindi.",
                    taskId = taskId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev silinirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = $"Görev silinirken bir hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return RedirectToAction("Login", "Account");

                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                    return NotFound();

                // Only project owner and managers can edit the project
                if (project.UserId != user.Id)
                {
                    var teamMember = await _context.ProjectTeamMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == user.Id);

                    if (teamMember == null || teamMember.Role != TeamMemberRole.Manager)
                        return Forbid();
                }

                // Projedeki bitiş tarihini loglayalım
                _logger.LogInformation($"Edit sayfası açılıyor: ProjectId={id}, EndDate={project.EndDate:yyyy-MM-dd}");

                // Layout için gereken bilgileri ViewData'da sakla
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = user.Email;
                ViewData["UserProfileImage"] = user.ProfileImageUrl;
                ViewData["CurrentUserId"] = user.Id;

                return View(project);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje düzenleme sayfası yüklenirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Proje düzenleme sayfası yüklenirken bir hata oluştu.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectModel model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return RedirectToAction("Login", "Account");

                // Layout için gereken bilgileri ViewData'da sakla
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = user.Email;
                ViewData["UserProfileImage"] = user.ProfileImageUrl;
                ViewData["CurrentUserId"] = user.Id;

                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                    return NotFound();

                // Only project owner and managers can edit the project
                if (project.UserId != user.Id)
                {
                    var teamMember = await _context.ProjectTeamMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == user.Id);

                    if (teamMember == null || teamMember.Role != TeamMemberRole.Manager)
                        return Forbid();
                }

                // Modeldeki değerleri loglayarak kontrol edelim
                _logger.LogInformation($"Form'dan gelen bitiş tarihi: {model.EndDate:yyyy-MM-dd}");

                // ModelState.IsValid koşulu yerine modelimize değer atamalarını direkt yapalım
                project.Name = model.Name;
                project.Description = model.Description;
                project.Status = model.Status;
                project.EndDate = model.EndDate;
                project.LastUpdatedDate = DateTime.UtcNow;
                project.Priority = model.Priority;
                
                if (string.IsNullOrEmpty(project.UserId))
                {
                    project.UserId = user.Id;
                }

                _logger.LogInformation($"Proje güncelleniyor: Bitiş Tarihi={project.EndDate:yyyy-MM-dd}");

                await _projectService.UpdateProjectAsync(project);
                TempData["SuccessMessage"] = "Proje başarıyla güncellendi.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje düzenlenirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Proje düzenlenirken bir hata oluştu.";
                
                // Hata durumunda değerleri loglayalım
                if (model != null)
                {
                    _logger.LogError($"Hata alınan form değerleri - EndDate: {model.EndDate:yyyy-MM-dd}");
                }
                
                return RedirectToAction(nameof(Edit), new { id });
            }
        }
    }
} 