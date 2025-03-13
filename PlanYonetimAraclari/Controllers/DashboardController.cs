using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Data;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(
            ILogger<DashboardController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _userManager = userManager;
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
            
            // Admin kullanıcıları admin paneline yönlendir
            if (userRole == "Admin")
            {
                _logger.LogInformation($"Admin kullanıcısı admin sayfasına yönlendiriliyor: {userEmail}");
                return RedirectToAction("Index", "Admin");
            }
            
            // Kullanıcıyı veritabanından bul ve profil resmini al
            var user = await _userManager.FindByEmailAsync(userEmail);
            string profileImageUrl = null;
            
            if (user != null)
            {
                profileImageUrl = user.ProfileImageUrl;
                _logger.LogInformation($"Kullanıcı profil resmi: {profileImageUrl ?? "Yok"}");
            }
            
            // Session'da profil resmi varsa onu kullan (yeni yüklendiyse daha güncel olacaktır)
            string sessionProfileImage = HttpContext.Session.GetString("UserProfileImage");
            if (!string.IsNullOrEmpty(sessionProfileImage))
            {
                profileImageUrl = sessionProfileImage;
                _logger.LogInformation($"Session'dan profil resmi alındı: {profileImageUrl}");
            }
            
            // Dashboard için gereken bilgileri ViewData'da sakla
            ViewData["UserFullName"] = userName;
            ViewData["UserEmail"] = userEmail;
            ViewData["UserProfileImage"] = profileImageUrl;
            
            _logger.LogInformation($"Kullanıcı başarıyla dashboard'a erişti: {userEmail}");
            return View();
        }
    }
} 

