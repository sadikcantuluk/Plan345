using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Data;

namespace PlanYonetimAraclari.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProfileController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            ILogger<ProfileController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // Profil görüntüleme sayfası
        public async Task<IActionResult> Index()
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi Profil sayfasına");
                return RedirectToAction("Login", "Account");
            }

            try 
            {
                // Kullanıcıyı veritabanından bulalım
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null) 
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    return RedirectToAction("Index", "Dashboard");
                }
                
                var model = new ProfileViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    ProfileImageUrl = user.ProfileImageUrl
                };
                
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profil bilgileri getirilirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Profil bilgileri getirilirken bir hata oluştu.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        // Profil bilgilerini güncelleme (Ad, Soyad, Email, Resim)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfileInfo(ProfileViewModel model, IFormFile profileImage)
        {
            _logger.LogInformation($"UpdateProfileInfo başladı. Model: {model?.Id}, Resim: {(profileImage != null ? $"{profileImage.FileName}, {profileImage.Length} bytes" : "Yok")}");
            
            // Şifre alanları için ModelState hatalarını temizle
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");
            // ProfileImageUrl alanı için hataları temizle
            ModelState.Remove("ProfileImageUrl");
            
            if (!ModelState.IsValid)
            {
                var stateErrors = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning($"ModelState geçersiz: {stateErrors}");
                return View("Index", model);
            }

            try
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                    return View("Index", model);
                }
                
                // Debug için loglama
                _logger.LogInformation($"Profil bilgileri güncelleme: Kullanıcı {user.Email}, Resim: {(profileImage != null ? "Var" : "Yok")}");
                
                // Herhangi bir bilgi güncellendiyse işaretleyecek bayrak
                bool isUserUpdated = false;
                
                // Ad ve soyad değerlerini güncelle
                if (user.FirstName != model.FirstName || user.LastName != model.LastName)
                {
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    isUserUpdated = true;
                    _logger.LogInformation($"Ad/Soyad güncellendi: {model.FirstName} {model.LastName}");
                }
                
                // Email değiştiyse
                if (user.Email != model.Email)
                {
                    user.Email = model.Email;
                    user.UserName = model.Email; // ASP.NET Identity genellikle username ve email'i senkronize eder
                    user.NormalizedEmail = model.Email.ToUpper();
                    user.NormalizedUserName = model.Email.ToUpper();
                    isUserUpdated = true;
                    _logger.LogInformation($"Email güncellendi: {model.Email}");
                }
                
                // Profil resmi yüklendiyse
                string profileImagePath = null;
                if (profileImage != null && profileImage.Length > 0)
                {
                    try
                    {
                        // Debug için loglama
                        _logger.LogInformation($"Profil resmi yükleniyor: {profileImage.FileName}, Boyut: {profileImage.Length} byte, ContentType: {profileImage.ContentType}");
                        
                        // Profil resimleri için klasör yolu
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profiles");
                        _logger.LogInformation($"Upload klasörü: {uploadsFolder}");
                        
                        // Klasör yoksa oluştur
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                            _logger.LogInformation($"Uploads klasörü oluşturuldu: {uploadsFolder}");
                        }
                        
                        // Benzersiz dosya adı oluştur
                        string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(profileImage.FileName)}";
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        
                        _logger.LogInformation($"Dosya kaydediliyor: {filePath}");
                        
                        // Dosyayı kaydet
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await profileImage.CopyToAsync(fileStream);
                        }
                        
                        // Dosya var mı kontrol et
                        if (System.IO.File.Exists(filePath))
                        {
                            _logger.LogInformation($"Dosya başarıyla kaydedildi: {filePath}");
                            
                            // Veritabanında yolu güncelle
                            profileImagePath = $"/images/profiles/{uniqueFileName}";
                            user.ProfileImageUrl = profileImagePath;
                            isUserUpdated = true;
                            
                            _logger.LogInformation($"Profil resmi URL'i veritabanında güncellendi: {user.ProfileImageUrl}");
                            
                            // Session'a profil resmi URL'ini ekle
                            HttpContext.Session.SetString("UserProfileImage", user.ProfileImageUrl);
                        }
                        else
                        {
                            _logger.LogError($"Dosya kaydedildi ancak bulunamadı: {filePath}");
                            ModelState.AddModelError("", "Profil resmi kaydedildi ancak dosya sisteminde bulunamadı.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Profil resmi yüklenirken hata oluştu: {ex.Message}, StackTrace: {ex.StackTrace}");
                        ModelState.AddModelError("", "Profil resmi yüklenirken bir hata oluştu: " + ex.Message);
                    }
                }
                
                // Kullanıcı bilgileri güncellendiyse veritabanında güncelle
                if (isUserUpdated)
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (updateResult.Succeeded)
                    {
                        // Session bilgilerini güncelle
                        HttpContext.Session.SetString("UserEmail", user.Email);
                        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                        if (!string.IsNullOrEmpty(profileImagePath))
                        {
                            HttpContext.Session.SetString("UserProfileImage", profileImagePath);
                        }
                        
                        TempData["SuccessMessage"] = "Profil bilgileriniz başarıyla güncellendi.";
                        _logger.LogInformation($"Kullanıcı {user.Email} profil bilgilerini başarıyla güncelledi");
                    }
                    else
                    {
                        foreach (var error in updateResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                            _logger.LogError($"Kullanıcı güncelleme hatası: {error.Description}");
                        }
                    }
                }
                else
                {
                    // Herhangi bir değişiklik yapılmadıysa bilgi mesajı göster
                    TempData["InfoMessage"] = "Profil bilgilerinizde herhangi bir değişiklik yapılmadı.";
                    _logger.LogInformation($"Kullanıcı {user.Email} için profil bilgilerinde değişiklik yapılmadı");
                }
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profil bilgileri güncellenirken hata oluştu: {ex.Message}, StackTrace: {ex.StackTrace}");
                ModelState.AddModelError("", "Profil bilgileri güncellenirken bir hata oluştu: " + ex.Message);
                return View("Index", model);
            }
        }
        
        // Şifre değiştirme işlemi (ayrı bir form)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePassword(ProfileViewModel model)
        {
            _logger.LogInformation($"UpdatePassword başladı. Model: {model?.Id}");
            
            // Şifre alanlarının doğruluğunu kontrol et
            if (string.IsNullOrEmpty(model.NewPassword))
            {
                ModelState.AddModelError("NewPassword", "Yeni şifre alanı zorunludur.");
                return View("Index", await GetProfileModel(model.Id));
            }
            
            if (string.IsNullOrEmpty(model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "Şifre tekrar alanı zorunludur.");
                return View("Index", await GetProfileModel(model.Id));
            }
            
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Şifreler eşleşmiyor.");
                return View("Index", await GetProfileModel(model.Id));
            }
            
            try
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                    return View("Index", await GetProfileModel(model.Id));
                }
                
                _logger.LogInformation("Şifre değiştirme işlemi başlatılıyor");
                
                // Önce token oluştur
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                _logger.LogInformation($"Reset token oluşturuldu, token uzunluğu: {token?.Length ?? 0}");
                
                // Şifreyi değiştir
                var resetPasswordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!resetPasswordResult.Succeeded)
                {
                    foreach (var error in resetPasswordResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                        _logger.LogError($"Şifre değiştirme hatası: {error.Description}");
                    }
                    return View("Index", await GetProfileModel(model.Id));
                }
                
                _logger.LogInformation($"Kullanıcı {user.Email} şifresini başarıyla değiştirdi");
                TempData["SuccessMessage"] = "Şifreniz başarıyla güncellenmiştir.";
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Şifre değiştirme işlemi sırasında hata: {ex.Message}, StackTrace: {ex.StackTrace}");
                ModelState.AddModelError("", "Şifre değiştirme sırasında bir hata oluştu: " + ex.Message);
                return View("Index", await GetProfileModel(model.Id));
            }
        }
        
        // Yardımcı metod: Profil modelini getir
        private async Task<ProfileViewModel> GetProfileModel(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return new ProfileViewModel();
                }
                
                return new ProfileViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    ProfileImageUrl = user.ProfileImageUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profil modeli getirilirken hata: {ex.Message}");
                return new ProfileViewModel { Id = userId };
            }
        }
    }
} 