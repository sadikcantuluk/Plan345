using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Services;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanYonetimAraclari.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ActivityService _activityService;
        private readonly ITaskService _taskService;
        private readonly ICalendarService _calendarService;
        private readonly ProfileImageHelper _profileImageHelper;

        public DashboardController(
            ILogger<DashboardController> logger,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            ActivityService activityService,
            ITaskService taskService,
            ICalendarService calendarService,
            ProfileImageHelper profileImageHelper)
        {
            _logger = logger;
            _userManager = userManager;
            _projectService = projectService;
            _activityService = activityService;
            _taskService = taskService;
            _calendarService = calendarService;
            _profileImageHelper = profileImageHelper;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Dashboard Index metodu çağrıldı");
            
            try
            {
                // Giriş yapmış bir kullanıcı olup olmadığını kontrol et
                string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
                
                if (User.Identity == null || !User.Identity.IsAuthenticated || isAuthenticated != "true")
                {
                    _logger.LogWarning("Unauthorized access attempt to dashboard");
                    return RedirectToAction("Login", "Account");
                }

                string userEmail = HttpContext.Session.GetString("UserEmail");
                string userName = HttpContext.Session.GetString("UserName");
                string userRole = HttpContext.Session.GetString("UserRole");
                
                _logger.LogInformation($"Dashboard açılıyor - Kullanıcı: {userEmail}, Rol: {userRole}");
                
                // Kullanıcı bilgilerini modele yükle
                var user = await _userManager.FindByEmailAsync(userEmail);
                
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    TempData["ErrorMessage"] = "Oturum bilgilerinizde bir hata oluştu. Lütfen tekrar giriş yapın.";
                    return RedirectToAction("Login", "Account");
                }

                // Profil resmi kontrolü
                string profileImageUrl = await _profileImageHelper.CheckAndUpdateProfileImage(user, HttpContext);

                // Kullanıcının projelerini getir
                var projects = await _projectService.GetUserProjectsAsync(user.Id);
                
                // Proje istatistiklerini al
                int totalProjectsCount = await _projectService.GetUserProjectsCountAsync(user.Id);
                int activeProjectsCount = await _projectService.GetUserActiveProjectsCountAsync(user.Id);
                
                // Tamamlanmış ve beklemedeki proje sayılarını al
                int completedProjectsCount = await _projectService.GetUserCompletedProjectsCountAsync(user.Id);
                int pendingProjectsCount = await _projectService.GetUserPendingProjectsCountAsync(user.Id);
                
                // Kullanıcının sahip olduğu proje sayısını al (maximum limitini karşılaştırmak için)
                int ownedProjectsCount = await _projectService.GetUserOwnedProjectsCountAsync(user.Id);
                
                // Son etkinlikleri getir
                var recentActivities = await _activityService.GetUserAndTeamActivitiesAsync(user.Id, 20);
                
                // Etkinlik yoksa ve kullanıcı etkinlikleri ilk kez görüntülüyorsa demo etkinlikleri göster
                string activityDemoShown = HttpContext.Session.GetString("ActivityDemoShown");
                if (!recentActivities.Any() && string.IsNullOrEmpty(activityDemoShown))
                {
                    // Demo etkinlikleri gösterdiğimizi session'a kaydet
                    HttpContext.Session.SetString("ActivityDemoShown", "true");
                    
                    // Demo etkinlikleri ekle (gösterim amaçlı - veritabanına kaydedilmez)
                    recentActivities = new List<ActivityLog>
                    {
                        new ActivityLog 
                        { 
                            Id = -1, 
                            UserId = user.Id,
                            Type = ActivityType.ProjectCreated, 
                            Title = "Yeni proje oluşturuldu", 
                            Description = "Pazarlama Kampanyası", 
                            CreatedAt = DateTime.Now.AddHours(-2) 
                        },
                        new ActivityLog 
                        { 
                            Id = -2, 
                            UserId = user.Id,
                            Type = ActivityType.TaskCompleted, 
                            Title = "Görev tamamlandı", 
                            Description = "Logo tasarımı", 
                            CreatedAt = DateTime.Now.AddHours(-5) 
                        },
                        new ActivityLog 
                        { 
                            Id = -3, 
                            UserId = user.Id,
                            Type = ActivityType.TaskUpdated, 
                            Title = "Görev güncellendi", 
                            Description = "İçerik düzenleme", 
                            CreatedAt = DateTime.Now.AddDays(-1) 
                        },
                        new ActivityLog 
                        { 
                            Id = -4, 
                            UserId = user.Id,
                            Type = ActivityType.NoteCreated, 
                            Title = "Yeni not eklendi", 
                            Description = "Toplantı notları", 
                            CreatedAt = DateTime.Now.AddDays(-2) 
                        }
                    };
                }
                
                // Kullanıcıya atanmış ve yaklaşan görevleri al
                var assignedTasks = await _taskService.GetUserAssignedTasksAsync(user.Id);
                
                // Kullanıcının yaklaşan takvim notlarını al
                var upcomingNotes = await _calendarService.GetUserUpcomingNotesAsync(user.Id);
                
                // Dashboard model oluştur
                var dashboardModel = new DashboardViewModel
                {
                    UserProfile = new ProfileViewModel
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        ProfileImageUrl = profileImageUrl
                    },
                    Projects = projects ?? new List<ProjectModel>(),
                    TotalProjectsCount = totalProjectsCount,
                    ActiveProjectsCount = activeProjectsCount,
                    CompletedProjectsCount = completedProjectsCount,
                    PendingProjectsCount = pendingProjectsCount,
                    RecentActivities = recentActivities,
                    ActivityService = _activityService,
                    AssignedTasks = assignedTasks ?? new List<TaskModel>(),
                    UpcomingCalendarNotes = upcomingNotes ?? new List<CalendarNote>()
                };
                
                // Layout için gereken bilgileri ViewData'da sakla (ProfileController ile tutarlı)
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = userEmail;
                ViewData["UserProfileImage"] = profileImageUrl;
                ViewBag.CurrentUserId = user.Id;
                
                // Kullanıcının limit bilgilerini ViewBag'e aktar
                ViewBag.MaxProjectsAllowed = user.MaxProjectsAllowed;
                ViewBag.OwnedProjectsCount = ownedProjectsCount;
                ViewBag.RemainingProjectsCount = user.MaxProjectsAllowed - ownedProjectsCount;
                ViewBag.MaxMembersPerProject = user.MaxMembersPerProject;
                
                return View(dashboardModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Dashboard Index metodunda hata oluştu: {ex.Message}");
                _logger.LogError($"Hata detayları: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                
                TempData["ErrorMessage"] = $"Dashboard Index metodunda bir hata oluştu: {ex.Message}";
                return RedirectToAction("Login", "Account");
            }
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProject(ProjectModel model)
        {
            _logger.LogInformation($"CreateProject metodu çağrıldı - Proje Adı: {model.Name}, Durum: {model.Status}, DueDate: {model.DueDate}");
            _logger.LogInformation($"Form verisi: Name={model?.Name ?? "null"}, Description={model?.Description ?? "null"}, Status={model?.Status}, DueDate={model?.DueDate}, CreatedDate={model?.CreatedDate}");
            
            // Request.Form verilerini kontrol et
            _logger.LogInformation("Request.Form değerleri:");
            foreach (var key in Request.Form.Keys)
            {
                _logger.LogInformation($"Form Key: {key}, Value: {Request.Form[key]}");
            }

            // ModelState hata durumunu detaylıca logla
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState geçerli değil. Hatalar:");
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogWarning($"- {state.Key}: {error.ErrorMessage}");
                    }
                }
            }
            
            // Session'dan kullanıcı bilgilerini al
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            // Kullanıcıyı bul
            var user = await _userManager.FindByEmailAsync(userEmail);
            
            if (user == null)
            {
                _logger.LogWarning($"Proje oluşturma - Kullanıcı bulunamadı: {userEmail}");
                return RedirectToAction("Login", "Account");
            }
            
            // Manuel doğrulama yap, ModelState.IsValid kontrolünü pas geç
            bool isValid = true;
            if (string.IsNullOrEmpty(model.Name))
            {
                isValid = false;
                _logger.LogWarning("Proje adı boş!");
                TempData["ErrorMessage"] = "Proje adı zorunludur.";
            }
            
            if (!isValid)
            {
                return RedirectToAction("Index");
            }
            
            try
            {
                // Kullanıcının proje limitini kontrol et - SADECE sahip olduğu projeler sayılır
                // DbContext'e doğrudan erişimimiz olmadığı için ProjectService üzerinden özel bir metod çağırıyoruz
                var userOwnedProjectsCount = await _projectService.GetUserOwnedProjectsCountAsync(user.Id);
                
                _logger.LogInformation($"Kullanıcı {user.Email} için proje sayısı: {userOwnedProjectsCount}, limit: {user.MaxProjectsAllowed}");
                
                if (userOwnedProjectsCount >= user.MaxProjectsAllowed)
                {
                    _logger.LogWarning($"Kullanıcı {user.Email} maksimum proje limitine ulaştı ({user.MaxProjectsAllowed})");
                    TempData["ErrorMessage"] = $"Maksimum proje limitine ulaştınız ({user.MaxProjectsAllowed}). Yeni proje oluşturmak için mevcut projelerinizden birini silmeniz veya admin ile iletişime geçmeniz gerekiyor.";
                    return RedirectToAction("Index");
                }
                
                // ModelState'i temizle - doğrulama hatalarını bypass et
                ModelState.Clear();
                
                // Null kontrolü ve düzeltmesi
                if (model.Description == null)
                {
                    model.Description = string.Empty;
                    _logger.LogInformation("Açıklama alanı null olduğu için boş string ile değiştirildi");
                }
                
                // Oluşturulma tarihini şu an olarak ayarla
                model.CreatedDate = DateTime.Now;
                _logger.LogInformation($"CreatedDate şu an olarak ayarlandı: {model.CreatedDate}");
                
                // Kullanıcı ID'yi ayarla
                model.UserId = user.Id;
                _logger.LogInformation($"Projeye UserId atandı: {model.UserId}");
                
                // Projeyi oluşturmaya çalış
                _logger.LogInformation("ProjectService.CreateProjectAsync metodu çağrılıyor...");
                var createdProject = await _projectService.CreateProjectAsync(model);
                
                if (createdProject != null && createdProject.Id > 0)
                {
                    _logger.LogInformation($"Proje başarıyla oluşturuldu: {createdProject.Id} - {createdProject.Name}");
                    TempData["SuccessMessage"] = "Proje başarıyla oluşturuldu.";
                    
                    // Etkinlik kaydı zaten ProjectService içinde oluşturuluyor, buradan kaldırıldı
                }
                else
                {
                    _logger.LogWarning("Projeye ID atanmadı veya geçersiz bir ID atandı");
                    TempData["WarningMessage"] = "Proje oluşturuldu ancak bilgileri tam olarak kaydedilememiş olabilir.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje oluşturulurken hata oluştu: {ex.Message}");
                _logger.LogError($"Hata detayları: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                
                TempData["ErrorMessage"] = $"Proje oluşturulurken bir hata oluştu: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }
        
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            // Session'dan kullanıcı bilgilerini al
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            // Kullanıcıyı bul
            var user = await _userManager.FindByEmailAsync(userEmail);
            
            if (user == null)
            {
                _logger.LogWarning($"Proje listeleme - Kullanıcı bulunamadı: {userEmail}");
                return Json(new { success = false, message = "Kullanıcı bilgileri bulunamadı." });
            }
            
            try
            {
                // Kullanıcının projelerini getir
                var projects = await _projectService.GetUserProjectsAsync(user.Id);
                return Json(new { success = true, data = projects });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Projeler listelenirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = "Projeler listelenirken bir hata oluştu." });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkTaskCompleted(int taskId)
        {
            try
            {
                // Kullanıcı ID'sini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                var user = await _userManager.FindByEmailAsync(userEmail);
                
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });
                }
                
                // Görevi tamamlandı olarak işaretle
                bool result = await _taskService.MarkTaskAsCompletedAsync(taskId, user.Id);
                
                if (result)
                {
                    return Json(new { success = true, message = "Görev tamamlandı olarak işaretlendi." });
                }
                else
                {
                    return Json(new { success = false, message = "Görev bulunamadı veya tamamlama yetkiniz bulunmuyor." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev tamamlanırken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = "Görev tamamlanırken bir hata oluştu." });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkNoteCompleted(int noteId)
        {
            try
            {
                // Kullanıcı ID'sini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                var user = await _userManager.FindByEmailAsync(userEmail);
                
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });
                }
                
                // Notu tamamlandı olarak işaretle
                bool result = await _calendarService.MarkNoteAsCompletedAsync(noteId, user.Id);
                
                if (result)
                {
                    return Json(new { success = true, message = "Not tamamlandı olarak işaretlendi." });
                }
                else
                {
                    return Json(new { success = false, message = "Not bulunamadı veya not sizin değil." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Not tamamlanırken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = "Not tamamlanırken bir hata oluştu." });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> SnoozeNote(int noteId, DateTime newDate)
        {
            try
            {
                // Kullanıcı ID'sini al
                string userEmail = HttpContext.Session.GetString("UserEmail");
                var user = await _userManager.FindByEmailAsync(userEmail);
                
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });
                }
                
                // Notu ertele
                bool result = await _calendarService.SnoozeNoteAsync(noteId, user.Id, newDate);
                
                if (result)
                {
                    return Json(new { success = true, message = "Not ertelendi." });
                }
                else
                {
                    return Json(new { success = false, message = "Not bulunamadı veya not sizin değil." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Not ertelenirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = "Not ertelenirken bir hata oluştu." });
            }
        }
    }
} 

