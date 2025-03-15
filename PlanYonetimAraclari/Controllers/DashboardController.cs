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

namespace PlanYonetimAraclari.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;

        public DashboardController(
            ILogger<DashboardController> logger,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService)
        {
            _logger = logger;
            _userManager = userManager;
            _projectService = projectService;
        }

        public async Task<IActionResult> Index()
        {
            // Session'dan kullanıcı bilgilerini al
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            
            // Kullanıcı giriş yapmamışsa login sayfasına yönlendir
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true")
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

            // Kullanıcının projelerini getir
            var projects = await _projectService.GetUserProjectsAsync(user.Id);
            
            // Proje istatistiklerini al
            int totalProjectsCount = await _projectService.GetUserProjectsCountAsync(user.Id);
            int activeProjectsCount = await _projectService.GetUserActiveProjectsCountAsync(user.Id);
            
            // Tamamlanmış ve beklemedeki proje sayılarını al
            int completedProjectsCount = await _projectService.GetUserCompletedProjectsCountAsync(user.Id);
            int pendingProjectsCount = await _projectService.GetUserPendingProjectsCountAsync(user.Id);
            
            // Dashboard model oluştur
            var dashboardModel = new DashboardViewModel
            {
                UserProfile = new ProfileViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    ProfileImageUrl = user.ProfileImageUrl
                },
                Projects = projects ?? new List<ProjectModel>(),
                TotalProjectsCount = totalProjectsCount,
                ActiveProjectsCount = activeProjectsCount,
                CompletedProjectsCount = completedProjectsCount,
                PendingProjectsCount = pendingProjectsCount
            };
            
            // Session'da profil resmi varsa onu kullan (yeni yüklendiyse daha güncel olacaktır)
            string profileImageUrl = user.ProfileImageUrl;
            string sessionProfileImage = HttpContext.Session.GetString("UserProfileImage");
            if (!string.IsNullOrEmpty(sessionProfileImage))
            {
                profileImageUrl = sessionProfileImage;
                _logger.LogInformation($"Session'dan profil resmi alındı: {profileImageUrl}");
            }
            
            // Layout için gereken bilgileri ViewData'da sakla (ProfileController ile tutarlı)
            ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
            ViewData["UserEmail"] = userEmail;
            ViewData["UserProfileImage"] = profileImageUrl;
            
            return View(dashboardModel);
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
    }
} 

