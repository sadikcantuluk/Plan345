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
                        Role = string.Join(", ", roles)
                    };
                    
                    ViewData[$"Roles_{user.Id}"] = string.Join(", ", roles);
                    userViewModels.Add(userViewModel);
                }

                return View(userViewModels);
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Kullanıcıları getirirken hata oluştu: {ex.Message}");
                ViewData["ErrorMessage"] = "Kullanıcı verileri yüklenirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return View(new List<UserViewModel>());
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

        // Admin profil düzenleme işlemini gerçekleştir
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
    }
} 