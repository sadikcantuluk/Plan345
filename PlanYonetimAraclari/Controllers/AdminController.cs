using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using PlanYonetimAraclari.Data;

namespace PlanYonetimAraclari.Controllers
{
    // [Authorize(Roles = "Admin")] - Şimdilik kaldırıyorum ve session kontrolü ekliyorum
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _dbContext;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger,
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            // Session kontrolü ekleyelim
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userRole = HttpContext.Session.GetString("UserRole");
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            _logger.LogInformation($"Admin Index erişim denemesi. Giriş durumu: {isAuthenticated}, Rol: {userRole}, Email: {userEmail}");
            
            // Session yoksa veya Admin değilse, giriş sayfasına yönlendir
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true" || userRole != "Admin")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi Admin paneline");
                return RedirectToAction("Login", "Account");
            }
            
            // Admin bilgilerini ViewData'ya ekleyelim
            ViewData["AdminEmail"] = userEmail;
            
            try 
            {
                // Tüm kullanıcıları veritabanından çekelim
                var users = await _userManager.Users.ToListAsync();
                _logger.LogInformation($"Veritabanından {users.Count} kullanıcı çekildi");
                
                var userViewModels = new List<UserViewModel>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var userViewModel = new UserViewModel
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Role = string.Join(", ", roles),
                        ProfileImageUrl = user.ProfileImageUrl,
                        CreatedDate = user.CreatedDate,
                        LastLoginTime = user.LastLoginTime,
                        IsEmailVerified = user.IsEmailVerified,
                        TwoFactorEnabled = user.TwoFactorEnabled
                    };
                    
                    ViewData[$"Roles_{user.Id}"] = string.Join(", ", roles);
                    userViewModels.Add(userViewModel);
                }

                // Özet istatistikleri hesapla ve AdminViewModel oluştur
                var stats = await GetUserStatisticsAsync();
                var adminViewModel = new AdminViewModel
                {
                    Users = await _userManager.Users.ToListAsync(),
                    TotalUsers = users.Count,
                    AdminUsers = users.Count(u => userViewModels.FirstOrDefault(vm => vm.Id == u.Id)?.Role.Contains("Admin") == true),
                    StandardUsers = users.Count(u => userViewModels.FirstOrDefault(vm => vm.Id == u.Id)?.Role.Contains("Admin") != true),
                    VerifiedUsers = users.Count(u => u.IsEmailVerified),
                    TotalProjects = _dbContext.Projects.Count(),
                    NewUserGrowth = users.Count > 0 ? (double)users.Count(u => u.CreatedDate >= DateTime.Now.AddDays(-30)) / users.Count * 100 : 0,
                    NewProjectGrowth = _dbContext.Projects.Count() > 0 ? (double)_dbContext.Projects.Count(p => p.CreatedDate >= DateTime.Now.AddDays(-30)) / _dbContext.Projects.Count() * 100 : 0
                };

                return View(adminViewModel);
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Kullanıcıları getirirken hata oluştu: {ex.Message}");
                ViewData["ErrorMessage"] = "Kullanıcı verileri yüklenirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return View(new AdminViewModel());
            }
        }
        
        // Admin profil düzenleme sayfasını göster
        public async Task<IActionResult> EditProfile()
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userRole = HttpContext.Session.GetString("UserRole");
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true" || userRole != "Admin")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi Admin profil düzenleme sayfasına");
                return RedirectToAction("Login", "Account");
            }

            try 
            {
                // Admin kullanıcısını veritabanından bulalım
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null) 
                {
                    _logger.LogWarning($"Admin kullanıcısı bulunamadı: {userEmail}");
                    return RedirectToAction("Index", "Admin");
                }
                
                var model = new EditUserViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email
                };
                
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profil bilgileri getirilirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Profil bilgileri getirilirken bir hata oluştu.";
                return RedirectToAction("Index", "Admin");
            }
        }

        // Admin profil düzenleme işlemi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                    return View(model);
                }
                
                // Ad ve soyad değerlerini güncelle
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                
                // Email değiştiyse
                if (user.Email != model.Email)
                {
                    user.Email = model.Email;
                    user.UserName = model.Email; // ASP.NET Identity genellikle username ve email'i senkronize eder
                    user.NormalizedEmail = model.Email.ToUpper();
                    user.NormalizedUserName = model.Email.ToUpper();
                }
                
                // Şifre değiştiyse
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    // Önce eski şifreyi kaldır
                    var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                    if (!removePasswordResult.Succeeded)
                    {
                        foreach (var error in removePasswordResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }
                    
                    // Sonra yeni şifreyi ekle
                    var addPasswordResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
                    if (!addPasswordResult.Succeeded)
                    {
                        foreach (var error in addPasswordResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }
                    
                    _logger.LogInformation($"Kullanıcı {user.Email} şifresini değiştirdi");
                }
                
                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    // Session bilgilerini güncelle
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                    
                    TempData["SuccessMessage"] = "Profil bilgileriniz başarıyla güncellendi.";
                    return RedirectToAction("Index", "Admin");
                }
                
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profil güncellenirken hata oluştu: {ex.Message}");
                ModelState.AddModelError("", "Profil güncellenirken bir hata oluştu: " + ex.Message);
                return View(model);
            }
        }

        // Kullanıcı silme işlemi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Json(new { success = false, message = "Geçersiz kullanıcı ID'si." });
            }

            try
            {
                // Kullanıcıyı al
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });
                }
                
                // Kullanıcının rollerini kontrol et
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                {
                    return Json(new { success = false, message = "Admin kullanıcıları silinemez." });
                }
                
                // Kullanıcının profil resmini silme işlemi
                if (!string.IsNullOrEmpty(user.ProfileImageUrl) && !user.ProfileImageUrl.Contains("default"))
                {
                    try
                    {
                        // Profil resim yolundan dosya sistemindeki tam yolu elde et
                        string profileImagePath = user.ProfileImageUrl.TrimStart('/');
                        string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, profileImagePath);
                        
                        // Dosya mevcutsa sil
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                            _logger.LogInformation($"Kullanıcı profil resmi silindi: {fullPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Profil resmi silinirken hata olursa loglayalım ama işleme devam edelim
                        _logger.LogWarning($"Kullanıcı profil resmi silinirken hata oluştu: {ex.Message}");
                    }
                }
                
                // Kullanıcıyı sil
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"Kullanıcı silindi: {user.Email}");
                    return Json(new { success = true, message = "Kullanıcı başarıyla silindi." });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Kullanıcı silinirken hata oluştu: {errors}");
                    return Json(new { success = false, message = errors });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı silinirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Kullanıcı detaylarını getiren API
        [HttpGet]
        public async Task<IActionResult> GetUserDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("Invalid user ID provided for detail view");
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {id} not found");
                return NotFound();
            }

            // Kullanıcının projelerinin sayısını al
            var projectCount = _dbContext.ProjectTeamMembers
                .Count(pm => pm.UserId == id);

            var userDetails = new AdminViewModel.UserDetailsViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsAdmin = await _userManager.IsInRoleAsync(user, "Admin"),
                IsEmailVerified = user.IsEmailVerified,
                RegisterDate = user.CreatedDate,
                LastLoginDate = user.LastLoginTime,
                ProfileImageUrl = user.ProfileImageUrl,
                ProjectCount = projectCount,
                MaxProjectsAllowed = user.MaxProjectsAllowed,
                MaxMembersPerProject = user.MaxMembersPerProject
            };

            return Json(userDetails);
        }
        
        // Kullanıcı rolünü değiştirme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserRole([FromBody] ChangeRoleViewModel model)
        {
            if (string.IsNullOrEmpty(model.UserId))
            {
                _logger.LogWarning("Invalid user ID provided for role change");
                return BadRequest("Geçersiz kullanıcı ID'si");
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {model.UserId} not found");
                return NotFound("Kullanıcı bulunamadı");
            }

            // Mevcut rolleri kontrol et
            var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var makeAdmin = model.Role == "Admin";

            // Sadece gerekiyorsa değişiklik yap
            if (isCurrentlyAdmin && !makeAdmin)
            {
                // Admin'den Kullanıcı'ya düşür
                await _userManager.RemoveFromRoleAsync(user, "Admin");
                await _userManager.AddToRoleAsync(user, "User");
                _logger.LogInformation($"User {user.UserName} demoted from Admin to User");
            }
            else if (!isCurrentlyAdmin && makeAdmin)
            {
                // Kullanıcı'dan Admin'e yükselt
                await _userManager.RemoveFromRoleAsync(user, "User");
                await _userManager.AddToRoleAsync(user, "Admin");
                _logger.LogInformation($"User {user.UserName} promoted from User to Admin");
            }
            // Değişiklik yoksa bir şey yapma

            return Ok("Rol başarıyla güncellendi");
        }
        
        // İki faktörlü doğrulamayı etkinleştirme/devre dışı bırakma
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTwoFactor(string userId, bool enable)
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userRole = HttpContext.Session.GetString("UserRole");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true" || userRole != "Admin")
            {
                return Json(new { success = false, message = "Yetkisiz erişim." });
            }
            
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });
                }
                
                user.TwoFactorEnabled = enable;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    string message = enable ? "İki faktörlü doğrulama etkinleştirildi." : "İki faktörlü doğrulama devre dışı bırakıldı.";
                    _logger.LogInformation($"Kullanıcı {user.Email} için iki faktörlü doğrulama: {enable}");
                    return Json(new { success = true, message = message });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"İki faktörlü doğrulama değiştirilirken hata oluştu: {errors}");
                    return Json(new { success = false, message = errors });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"İki faktörlü doğrulama değiştirilirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        // Kullanıcı istatistiklerini getiren API
        [HttpGet]
        public async Task<IActionResult> GetUserStatistics()
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userRole = HttpContext.Session.GetString("UserRole");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true" || userRole != "Admin")
            {
                return Json(new { success = false, message = "Yetkisiz erişim." });
            }
            
            try
            {
                var stats = await GetUserStatisticsAsync();
                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı istatistikleri getirilirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        // Kullanıcı istatistiklerini getiren yardımcı metod
        private async Task<object> GetUserStatisticsAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            
            // Admin kullanıcılarını bul
            int adminCount = 0;
            int userCount = 0;
            int verifiedCount = 0;
            int twoFactorEnabledCount = 0;
            
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                
                if (roles.Contains("Admin"))
                    adminCount++;
                else
                    userCount++;
                
                if (user.IsEmailVerified)
                    verifiedCount++;
                
                if (user.TwoFactorEnabled)
                    twoFactorEnabledCount++;
            }
            
            // Son 7 günde kayıt olan kullanıcıları bul
            var lastWeekRegistrations = users.Count(u => u.CreatedDate >= DateTime.Now.AddDays(-7));
            
            // Son 30 günde kayıt olan kullanıcıları bul
            var lastMonthRegistrations = users.Count(u => u.CreatedDate >= DateTime.Now.AddDays(-30));
            
            // Aktif proje sayısı
            var activeProjectCount = _dbContext.Projects.Count(p => p.Status != ProjectStatus.Completed);
            
            // Tamamlanan proje sayısı
            var completedProjectCount = _dbContext.Projects.Count(p => p.Status == ProjectStatus.Completed);
            
            return new
            {
                totalUsers = users.Count,
                adminCount = adminCount,
                userCount = userCount,
                verifiedUsers = verifiedCount,
                twoFactorEnabled = twoFactorEnabledCount,
                lastWeekRegistrations = lastWeekRegistrations,
                lastMonthRegistrations = lastMonthRegistrations,
                activeProjects = activeProjectCount,
                completedProjects = completedProjectCount
            };
        }

        // Kullanıcı proje ve üye limitlerini güncelleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserLimits([FromBody] UpdateUserLimitsViewModel model)
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userRole = HttpContext.Session.GetString("UserRole");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true" || userRole != "Admin")
            {
                return Json(new { success = false, message = "Yetkisiz erişim." });
            }
            
            try
            {
                if (string.IsNullOrEmpty(model.UserId))
                {
                    return Json(new { success = false, message = "Geçersiz kullanıcı ID'si" });
                }

                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı" });
                }
                
                // Limit değerlerinin geçerli olduğunu kontrol et
                if (model.MaxProjectsAllowed < 1)
                {
                    return Json(new { success = false, message = "İzin verilen maksimum proje sayısı en az 1 olmalıdır." });
                }
                
                if (model.MaxMembersPerProject < 1)
                {
                    return Json(new { success = false, message = "Proje başına izin verilen maksimum üye sayısı en az 1 olmalıdır." });
                }
                
                // Limitleri güncelle
                user.MaxProjectsAllowed = model.MaxProjectsAllowed;
                user.MaxMembersPerProject = model.MaxMembersPerProject;
                
                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"Kullanıcı {user.Email} limitleri güncellendi: MaxProjects={model.MaxProjectsAllowed}, MaxMembers={model.MaxMembersPerProject}");
                    return Json(new { 
                        success = true, 
                        message = "Kullanıcı limitleri başarıyla güncellendi.",
                        maxProjectsAllowed = user.MaxProjectsAllowed,
                        maxMembersPerProject = user.MaxMembersPerProject 
                    });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Kullanıcı limitleri güncellenirken hata oluştu: {errors}");
                    return Json(new { success = false, message = errors });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı limitleri güncellenirken hata oluştu: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Kullanıcı proje ve üye limitleri için model
        public class UpdateUserLimitsViewModel
        {
            public string UserId { get; set; }
            public int MaxProjectsAllowed { get; set; }
            public int MaxMembersPerProject { get; set; }
        }

        public class ChangeRoleViewModel
        {
            public string UserId { get; set; }
            public string Role { get; set; }
        }
    }
} 