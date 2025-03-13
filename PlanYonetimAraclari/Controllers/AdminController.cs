using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace PlanYonetimAraclari.Controllers
{
    // [Authorize(Roles = "Admin")] - Şimdilik kaldırıyorum ve session kontrolü ekliyorum
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Session kontrolü ekleyelim
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userRole = HttpContext.Session.GetString("UserRole");
            
            _logger.LogInformation($"Admin Index erişim denemesi. Giriş durumu: {isAuthenticated}, Rol: {userRole}");
            
            // Session yoksa veya Admin değilse, giriş sayfasına yönlendir
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true" || userRole != "Admin")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi Admin paneline");
                return RedirectToAction("Login", "Account");
            }
            
            // Eğer Identity ile giriş yapıldıysa
            if (User.Identity?.IsAuthenticated == true)
            {
                var users = _userManager.Users.ToList();
                var userViewModels = new List<UserViewModel>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var userViewModel = new UserViewModel
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email
                    };
                    
                    ViewData[$"Roles_{user.Id}"] = string.Join(", ", roles);
                    userViewModels.Add(userViewModel);
                }

                return View(userViewModels);
            }
            
            // Test için örnek kullanıcı listesi oluşturalım
            var testUserViewModels = new List<UserViewModel>
            {
                new UserViewModel { Id = "1", FirstName = "Admin", LastName = "Kullanıcı", Email = "admin@plan345.com" },
                new UserViewModel { Id = "2", FirstName = "Test", LastName = "Kullanıcısı", Email = "user@plan345.com" },
                new UserViewModel { Id = "3", FirstName = "Demo", LastName = "Kullanıcı", Email = "demo@plan345.com" }
            };
            
            ViewData["Roles_1"] = "Admin";
            ViewData["Roles_2"] = "User";
            ViewData["Roles_3"] = "User";
            
            _logger.LogInformation("Admin sayfası örnek verilerle yüklendi");
            return View(testUserViewModels);
        }
    }
} 