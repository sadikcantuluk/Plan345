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

namespace PlanYonetimAraclari.Controllers
{
    public class ProjectController : Controller
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ApplicationDbContext _context;

        public ProjectController(
            ILogger<ProjectController> logger,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            ApplicationDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _projectService = projectService;
            _context = context;
        }
        
        // Proje detayları sayfası
        public async Task<IActionResult> Details(int id)
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi proje detaylarına");
                return RedirectToAction("Login", "Account");
            }
            
            try
            {
                // Session'dan kullanıcı bilgilerini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                
                // Kullanıcıyı bul
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    return RedirectToAction("Login", "Account");
                }
                
                // Proje detaylarını getir
                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                {
                    _logger.LogWarning($"Proje bulunamadı: {id}");
                    TempData["ErrorMessage"] = "Proje bulunamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Projenin sahibi mi kontrol et
                if (project.UserId != user.Id)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının ({project.UserId}) projesine erişmeye çalışıyor.");
                    TempData["ErrorMessage"] = "Bu projeye erişim yetkiniz yok.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Proje görevlerini getir
                List<TaskModel> todoTasks = new List<TaskModel>();
                List<TaskModel> inProgressTasks = new List<TaskModel>();
                List<TaskModel> doneTasks = new List<TaskModel>();
                
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
                
                // Session'da profil resmi varsa onu kullan
                string profileImageUrl = user.ProfileImageUrl;
                string sessionProfileImage = HttpContext.Session.GetString("UserProfileImage");
                if (!string.IsNullOrEmpty(sessionProfileImage))
                {
                    profileImageUrl = sessionProfileImage;
                }
                
                // Layout için gereken bilgileri ViewData'da sakla (DashboardController ile tutarlı)
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = userEmail;
                ViewData["UserProfileImage"] = profileImageUrl;
                
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje detayları görüntülenirken hata oluştu: {ex.Message}");
                _logger.LogError($"Hata ayrıntıları: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"İç hata: {ex.InnerException.Message}");
                }
                
                TempData["ErrorMessage"] = "Proje detayları görüntülenirken bir hata oluştu.";
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
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının ({project.UserId}) projesini silmeye çalışıyor.");
                    return Json(new { success = false, message = "Bu projeyi silme yetkiniz yok." });
                }
                
                // Projeyi sil (tüm görevlerle birlikte)
                var result = await _projectService.DeleteProjectAsync(id, true);
                if (result)
                {
                    _logger.LogInformation($"Proje başarıyla silindi: {id}");
                    return Json(new { success = true, message = "Proje başarıyla silindi." });
                }
                else
                {
                    _logger.LogWarning($"Proje silinirken bir hata oluştu: {id}");
                    return Json(new { success = false, message = "Proje silinirken bir hata oluştu." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje silinirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = $"Proje silinirken bir hata oluştu: {ex.Message}" });
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
                
                // Projenin sahibi mi kontrol et
                if (project.UserId != user.Id)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının ({project.UserId}) projesine görev eklemeye çalışıyor.");
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
                
                // Proje sahibi kontrolü
                var project = await _projectService.GetProjectDetailsByIdAsync(task.ProjectId);
                if (project.UserId != user.Id)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının projesindeki görevi güncellemeye çalışıyor.");
                    return Json(new { success = false, message = "Bu görevi güncelleme yetkiniz yok." });
                }
                
                // Görev durumunu güncelle
                task.Status = newStatus;
                task.LastUpdatedDate = DateTime.Now;
                
                var updatedTask = await _projectService.UpdateTaskAsync(task);
                
                return Json(new { 
                    success = true, 
                    message = "Görev durumu başarıyla güncellendi.",
                    task = new {
                        id = updatedTask.Id,
                        name = updatedTask.Name,
                        description = updatedTask.Description,
                        status = updatedTask.Status,
                        priority = updatedTask.Priority
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
                
                // Proje sahibi kontrolü
                var project = await _projectService.GetProjectDetailsByIdAsync(task.ProjectId);
                if (project.UserId != user.Id)
                {
                    _logger.LogWarning($"Kullanıcı ({user.Id}) başka bir kullanıcının projesindeki görevi silmeye çalışıyor.");
                    return Json(new { success = false, message = "Bu görevi silme yetkiniz yok." });
                }
                
                // Görevi sil
                var result = await _projectService.DeleteTaskAsync(taskId);
                
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
    }
} 