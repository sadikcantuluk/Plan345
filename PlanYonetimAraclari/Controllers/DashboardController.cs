using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ILogger<DashboardController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
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
            
            // Dashboard için gereken bilgileri ViewData'da sakla
            ViewData["UserFullName"] = userName;
            ViewData["UserEmail"] = userEmail;
            
            _logger.LogInformation($"Kullanıcı başarıyla dashboard'a erişti: {userEmail}");
            return View();
        }
    }
} 

