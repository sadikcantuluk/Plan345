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

                var projectDetails = await _projectService.GetProjectDetailsByIdAsync(id);
                if (projectDetails == null)
                {
                    TempData["ErrorMessage"] = "Proje bulunamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Kullanıcı projenin sahibi veya ekip üyesi mi kontrol et
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == id && m.UserId == user.Id);

                // Ekip üyeleri bilgisini getir
                var teamMembers = await _context.ProjectTeamMembers
                    .Where(m => m.ProjectId == id)
                    .Select(m => new ProjectTeamMemberViewModel
                    {
                        MemberId = m.Id,
                        ProjectId = m.ProjectId,
                        UserId = m.UserId,
                        UserName = m.User.UserName,
                        UserEmail = m.User.Email,
                        UserFullName = m.User.FullName,
                        UserProfileImage = m.User.ProfileImageUrl,
                        Role = m.Role
                    })
                    .ToListAsync();
                
                // Eğer kullanıcı proje sahibi ise onu da listeye ekle
                if (!teamMembers.Any(m => m.UserId == projectDetails.UserId))
                {
                    var projectOwner = await _userManager.FindByIdAsync(projectDetails.UserId);
                    if (projectOwner != null)
                    {
                        teamMembers.Add(new ProjectTeamMemberViewModel
                        {
                            ProjectId = id,
                            UserId = projectOwner.Id,
                            UserName = projectOwner.UserName,
                            UserEmail = projectOwner.Email,
                            UserFullName = projectOwner.FullName,
                            UserProfileImage = projectOwner.ProfileImageUrl,
                            Role = TeamMemberRole.Manager
                        });
                    }
                }
                
                if (projectDetails.UserId != user.Id && !isTeamMember)
                {
                    TempData["ErrorMessage"] = "Bu projeyi görüntüleme yetkiniz yok.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Kullanıcının rolünü belirle
                var userRole = TeamMemberRole.Member; // Varsayılan
                if (projectDetails.UserId == user.Id)
                {
                    userRole = TeamMemberRole.Owner; // Proje sahibi - Owner olarak düzeltildi
                }
                else if (isTeamMember)
                {
                    // Kullanıcının rolünü DB'den al
                    var member = await _context.ProjectTeamMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == user.Id);
                    if (member != null)
                    {
                        userRole = member.Role;
                    }
                }
                
                // View model'i doldur
                var viewModel = new ProjectDetailViewModel
                {
                    Project = projectDetails,
                    TodoTasks = await _context.Tasks.Where(t => t.ProjectId == id && t.Status == Models.TaskStatus.Todo).ToListAsync(),
                    InProgressTasks = await _context.Tasks.Where(t => t.ProjectId == id && t.Status == Models.TaskStatus.InProgress).ToListAsync(),
                    DoneTasks = await _context.Tasks.Where(t => t.ProjectId == id && t.Status == Models.TaskStatus.Done).ToListAsync(),
                    TeamMembers = teamMembers
                };
                
                // Düzenleme yetkisi kontrolü (Proje sahibi, Owner, Manager ve Member rolüne sahip üyeler)
                bool canModifyTasks = true; // Tüm üyelere ve sahiplere izin ver
                ViewBag.CanModifyTasks = canModifyTasks;
                ViewBag.UserRole = userRole;
                ViewBag.CurrentUserId = user.Id;
                
                // Layout için gereken bilgileri ViewData'da sakla
                ViewData["UserFullName"] = user.FullName;
                ViewData["UserEmail"] = user.Email;
                ViewData["UserProfileImage"] = user.ProfileImageUrl;
                ViewData["CurrentUserId"] = user.Id;
                
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje detayları görüntülenirken hata: {ex.Message}");
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
                // Kullanıcıyı bul
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Kullanıcı bulunamadı.");
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
                // Kullanıcıyı bul
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Kullanıcı bulunamadı.");
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
                
                // Projenin sahibi veya ekip üyesi olup olmadığını kontrol et - Tüm ekip üyelerine izin ver
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == model.ProjectId && m.UserId == user.Id);
                
                // Proje sahibi veya herhangi bir ekip üyesi görev ekleyebilir
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
                
                // Atanan üye bilgilerini getir (eğer atanmışsa)
                ProjectTeamMemberViewModel assignedMember = null;
                if (!string.IsNullOrEmpty(task.AssignedMemberId))
                {
                    var memberRecord = await _context.ProjectTeamMembers
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id.ToString() == task.AssignedMemberId);
                        
                    if (memberRecord != null)
                    {
                        assignedMember = new ProjectTeamMemberViewModel
                        {
                            MemberId = memberRecord.Id,
                            UserId = memberRecord.UserId,
                            UserName = memberRecord.User.UserName,
                            UserFullName = memberRecord.User.FullName,
                            UserEmail = memberRecord.User.Email,
                            UserProfileImage = memberRecord.User.ProfileImageUrl,
                            Role = memberRecord.Role
                        };
                    }
                }
                
                // SignalR ile yeni görevi tüm bağlı kullanıcılara bildir
                var taskForSignalR = new 
                {
                    task = task,
                    assignedMember = assignedMember
                };
                
                await _taskHub.Clients.Group($"project_{model.ProjectId}").SendAsync("ReceiveNewTask", taskForSignalR);
                
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
                // Kullanıcıyı bul
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Kullanıcı bulunamadı.");
                    return Json(new { success = false, message = "Kullanıcı bilgileri bulunamadı." });
                }
                
                // Görevi doğrudan veritabanından ID'ye göre getir
                var task = await _context.Tasks.FindAsync(taskId);
                
                if (task == null)
                {
                    _logger.LogWarning($"Görev bulunamadı: {taskId}");
                    return Json(new { success = false, message = "Görev bulunamadı." });
                }
                
                // Proje sahibi veya ekip üyesi kontrolü - Tüm ekip üyelerine izin ver
                var project = await _projectService.GetProjectDetailsByIdAsync(task.ProjectId);
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == user.Id);
                    
                // Proje sahibi veya herhangi bir ekip üyesi görev güncelleyebilir
                if (project.UserId != user.Id && !isTeamMember)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) görevi güncelleme yetkisine sahip değil.");
                    return Json(new { success = false, message = "Bu görevi güncelleme yetkiniz yok." });
                }
                
                // Görev durumunu güncelle
                task.Status = newStatus;
                task.LastUpdatedDate = DateTime.Now;
                
                var updatedTask = await _projectService.UpdateTaskAsync(task);
                
                // Atanan üye bilgilerini getir (eğer atanmışsa)
                ProjectTeamMemberViewModel assignedMember = null;
                if (!string.IsNullOrEmpty(updatedTask.AssignedMemberId))
                {
                    var memberRecord = await _context.ProjectTeamMembers
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id.ToString() == updatedTask.AssignedMemberId);
                        
                    if (memberRecord != null)
                    {
                        assignedMember = new ProjectTeamMemberViewModel
                        {
                            MemberId = memberRecord.Id,
                            UserId = memberRecord.UserId,
                            UserName = memberRecord.User.UserName,
                            UserFullName = memberRecord.User.FullName,
                            UserEmail = memberRecord.User.Email,
                            UserProfileImage = memberRecord.User.ProfileImageUrl,
                            Role = memberRecord.Role
                        };
                    }
                }
                
                // SignalR ile görev durumu değişikliğini tüm bağlı kullanıcılara bildir
                var taskForSignalR = new 
                {
                    task = updatedTask,
                    assignedMember = assignedMember
                };
                
                await _taskHub.Clients.Group($"project_{task.ProjectId}").SendAsync("ReceiveTaskStatusChange", taskId, newStatus, taskForSignalR);
                
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
                    },
                    assignedMember = assignedMember
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
                // Kullanıcıyı bul
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Kullanıcı bulunamadı.");
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
                
                // Proje sahibi veya ekip üyesi kontrolü - Tüm ekip üyelerine izin ver
                var project = await _projectService.GetProjectDetailsByIdAsync(task.ProjectId);
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == user.Id);
                
                // Proje sahibi veya herhangi bir ekip üyesi görev silebilir
                if (project.UserId != user.Id && !isTeamMember)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının projesindeki görevi silmeye çalışıyor.");
                    return Json(new { success = false, message = "Bu görevi silme yetkiniz yok." });
                }
                
                // Görevi sil
                var result = await _projectService.DeleteTaskAsync(taskId);
                
                // SignalR ile görev silme işlemini tüm bağlı kullanıcılara bildir
                await _taskHub.Clients.Group($"project_{projectId}").SendAsync("ReceiveTaskDelete", taskId);
                
                return Json(new { success = true, message = "Görev başarıyla silindi." });
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

        // Görev güncelleme (AJAX)
        [HttpPost]
        public async Task<IActionResult> UpdateTask([FromBody] TaskModel model)
        {
            try
            {
                // Kullanıcıyı bul
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Kullanıcı bulunamadı.");
                    return Json(new { success = false, message = "Kullanıcı bilgileri bulunamadı." });
                }
                
                // Görevi veritabanından ID'ye göre getir
                var existingTask = await _context.Tasks.FindAsync(model.Id);
                
                if (existingTask == null)
                {
                    _logger.LogWarning($"Görev bulunamadı: {model.Id}");
                    return Json(new { success = false, message = "Görev bulunamadı." });
                }
                
                // Proje sahibi veya ekip üyesi kontrolü - Tüm ekip üyelerine izin ver
                var project = await _projectService.GetProjectDetailsByIdAsync(existingTask.ProjectId);
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == existingTask.ProjectId && m.UserId == user.Id);
                    
                // Proje sahibi veya herhangi bir ekip üyesi görev güncelleyebilir
                if (project.UserId != user.Id && !isTeamMember)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) görevi güncelleme yetkisine sahip değil.");
                    return Json(new { success = false, message = "Bu görevi güncelleme yetkiniz yok." });
                }
                
                // Görev özelliklerini güncelle
                existingTask.Name = model.Name;
                existingTask.Description = model.Description;
                existingTask.Priority = model.Priority;
                existingTask.DueDate = model.DueDate;
                existingTask.AssignedMemberId = model.AssignedMemberId;
                existingTask.LastUpdatedDate = DateTime.Now;
                
                // Görevi güncelle
                var updatedTask = await _projectService.UpdateTaskAsync(existingTask);
                
                // Atanan üye bilgilerini getir (eğer atanmışsa)
                ProjectTeamMemberViewModel assignedMember = null;
                if (!string.IsNullOrEmpty(updatedTask.AssignedMemberId))
                {
                    var memberRecord = await _context.ProjectTeamMembers
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(m => m.Id.ToString() == updatedTask.AssignedMemberId);
                        
                    if (memberRecord != null)
                    {
                        assignedMember = new ProjectTeamMemberViewModel
                        {
                            MemberId = memberRecord.Id,
                            UserId = memberRecord.UserId,
                            UserName = memberRecord.User.UserName,
                            UserFullName = memberRecord.User.FullName,
                            UserEmail = memberRecord.User.Email,
                            UserProfileImage = memberRecord.User.ProfileImageUrl,
                            Role = memberRecord.Role
                        };
                    }
                }
                
                // SignalR ile görev güncellemesini tüm bağlı kullanıcılara bildir
                var taskForSignalR = new 
                {
                    task = updatedTask,
                    assignedMember = assignedMember
                };
                
                await _taskHub.Clients.Group($"project_{existingTask.ProjectId}").SendAsync("ReceiveTaskUpdate", updatedTask.Id, taskForSignalR);
                
                return Json(new { 
                    success = true, 
                    message = "Görev başarıyla güncellendi.",
                    task = new {
                        id = updatedTask.Id,
                        name = updatedTask.Name,
                        description = updatedTask.Description,
                        status = updatedTask.Status,
                        priority = updatedTask.Priority,
                        dueDate = updatedTask.DueDate,
                        assignedMemberId = updatedTask.AssignedMemberId
                    },
                    assignedMember = assignedMember
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev güncellenirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = $"Görev güncellenirken bir hata oluştu: {ex.Message}" });
            }
        }

        // Görev getirme (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetTask(int taskId)
        {
            try
            {
                _logger.LogInformation($"GetTask çağrıldı: TaskId={taskId}");
                
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("GetTask: Kullanıcı bulunamadı");
                    return Json(null);
                }
                
                _logger.LogInformation($"GetTask: Kullanıcı bilgisi: UserId={user.Id}, Email={user.Email}");
                
                // Görevi veritabanından ID'ye göre getir
                var task = await _context.Tasks.FindAsync(taskId);
                
                if (task == null)
                {
                    _logger.LogWarning($"GetTask: Görev bulunamadı: {taskId}");
                    return Json(null);
                }
                
                _logger.LogInformation($"GetTask: Görev bilgisi: TaskId={task.Id}, ProjectId={task.ProjectId}, Name={task.Name}");
                
                // Proje sahibi veya ekip üyesi kontrolü - Tüm ekip üyelerine izin ver
                var isTeamMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == user.Id);
                    
                var project = await _projectService.GetProjectDetailsByIdAsync(task.ProjectId);
                
                _logger.LogInformation($"GetTask: İzin kontrolü: IsTeamMember={isTeamMember}, ProjectOwnerId={project.UserId}, CurrentUserId={user.Id}");
                
                // Proje sahibi veya herhangi bir ekip üyesi görev detaylarını görebilir
                if (project.UserId != user.Id && !isTeamMember)
                {
                    _logger.LogWarning($"GetTask: Kullanıcı ({user.Id}) görev görüntüleme yetkisine sahip değil");
                    return Json(null);
                }
                
                // Görevi döndür
                var result = new { 
                    id = task.Id,
                    projectId = task.ProjectId,
                    name = task.Name,
                    description = task.Description,
                    status = (int)task.Status,
                    priority = (int)task.Priority,
                    dueDate = task.DueDate,
                    assignedMemberId = task.AssignedMemberId
                };
                
                _logger.LogInformation($"GetTask: Görev bilgisi başarıyla döndürüldü: {task.Id}");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev bilgileri alınırken hata oluştu: {ex.Message}");
                return Json(null);
            }
        }
    }
} 