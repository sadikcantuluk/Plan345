using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Data;
using System.Linq;

namespace PlanYonetimAraclari.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProfileController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _dbContext;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            ILogger<ProfileController> logger,
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _dbContext = dbContext;
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
                    ProfileImageUrl = user.ProfileImageUrl,
                    TwoFactorEnabled = user.TwoFactorEnabled,
                    User = user
                };
                
                // Session'dan kullanıcı bilgilerini al
                string userName = HttpContext.Session.GetString("UserName");
                
                // Session'da profil resmi varsa onu kullan (yeni yüklendiyse daha güncel olacaktır)
                string profileImageUrl = user.ProfileImageUrl;
                string sessionProfileImage = HttpContext.Session.GetString("UserProfileImage");
                if (!string.IsNullOrEmpty(sessionProfileImage))
                {
                    profileImageUrl = sessionProfileImage;
                    _logger.LogInformation($"Session'dan profil resmi alındı: {profileImageUrl}");
                }
                
                // Layout için gereken bilgileri ViewData'da sakla (DashboardController ile aynı)
                ViewData["UserFullName"] = userName ?? $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = userEmail;
                ViewData["UserProfileImage"] = profileImageUrl;
                
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
        public async Task<IActionResult> UpdateProfileInfo(ProfileViewModel model, IFormFile ProfileImage)
        {
            _logger.LogInformation($"UpdateProfileInfo başladı. Model: {model?.Id}, Resim: {(ProfileImage != null ? $"{ProfileImage.FileName}, {ProfileImage.Length} bytes" : "Yok")}");
            
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
                // Kullanıcıyı bul - Eğer model.Id boş ise, session'daki e-posta ile kullanıcıyı bulalım
                var user = string.IsNullOrEmpty(model.Id) 
                    ? await _userManager.FindByEmailAsync(userEmail)
                    : await _userManager.FindByIdAsync(model.Id);
                
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı. ID: {model.Id}, Email: {userEmail}");
                    TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Formdaki inputlar boş geldiyse, mevcut değerleri koru
                // Ad boş ise mevcut adı koru, soyad boş ise mevcut soyadı koru
                var firstName = !string.IsNullOrWhiteSpace(model.FirstName) ? model.FirstName : user.FirstName;
                var lastName = !string.IsNullOrWhiteSpace(model.LastName) ? model.LastName : user.LastName;
                var email = !string.IsNullOrWhiteSpace(model.Email) ? model.Email : user.Email;
                
                // Debug için loglama
                _logger.LogInformation($"Profil bilgileri güncelleme: Kullanıcı {user.Email}, İsim: {firstName}, Soyisim: {lastName}, Resim: {(ProfileImage != null ? "Var" : "Yok")}");
                
                // Herhangi bir bilgi güncellendiyse işaretleyecek bayrak
                bool isUserUpdated = false;
                
                // Ad ve soyad değerlerini güncelle
                if (firstName != user.FirstName)
                {
                    user.FirstName = firstName;
                    isUserUpdated = true;
                    _logger.LogInformation($"Ad güncellendi: {firstName}");
                }
                
                if (lastName != user.LastName)
                {
                    user.LastName = lastName;
                    isUserUpdated = true;
                    _logger.LogInformation($"Soyad güncellendi: {lastName}");
                }
                
                // Email değiştiyse - ancak UI'da readonly olduğu için muhtemelen değişmeyecek
                if (email != user.Email && !string.IsNullOrWhiteSpace(email))
                {
                    user.Email = email;
                    user.UserName = email; // ASP.NET Identity genellikle username ve email'i senkronize eder
                    user.NormalizedEmail = email.ToUpper();
                    user.NormalizedUserName = email.ToUpper();
                    isUserUpdated = true;
                    _logger.LogInformation($"Email güncellendi: {email}");
                }
                
                // Profil resmi yüklendiyse
                string profileImagePath = null;
                if (ProfileImage != null && ProfileImage.Length > 0)
                {
                    try
                    {
                        // Debug için loglama
                        _logger.LogInformation($"Profil resmi yükleniyor: {ProfileImage.FileName}, Boyut: {ProfileImage.Length} byte, ContentType: {ProfileImage.ContentType}");
                        
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
                        string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(ProfileImage.FileName)}";
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        
                        _logger.LogInformation($"Dosya kaydediliyor: {filePath}");
                        
                        // Dosyayı kaydet
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await ProfileImage.CopyToAsync(fileStream);
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
                            TempData["ErrorMessage"] = "Profil resmi kaydedildi ancak dosya sisteminde bulunamadı.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Profil resmi yüklenirken hata oluştu: {ex.Message}, StackTrace: {ex.StackTrace}");
                        TempData["ErrorMessage"] = "Profil resmi yüklenirken bir hata oluştu: " + ex.Message;
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
                        
                        // Header için ViewData'yı güncelle
                        ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                        ViewData["UserEmail"] = user.Email;
                        ViewData["UserProfileImage"] = !string.IsNullOrEmpty(profileImagePath) 
                            ? profileImagePath 
                            : user.ProfileImageUrl;
                        
                        TempData["SuccessMessage"] = "Profil bilgileriniz başarıyla güncellendi.";
                        _logger.LogInformation($"Kullanıcı {user.Email} profil bilgilerini başarıyla güncelledi");
                    }
                    else
                    {
                        string errorMessage = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                        _logger.LogError($"Kullanıcı güncelleme hataları: {errorMessage}");
                        TempData["ErrorMessage"] = $"Profil bilgileri güncellenirken hata oluştu: {errorMessage}";
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
                TempData["ErrorMessage"] = "Profil bilgileri güncellenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
        
        // Şifre değiştirme işlemi (ayrı bir form)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePassword(ProfileViewModel model)
        {
            _logger.LogInformation($"UpdatePassword başladı. Model: {model?.Id}");
            
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi Profil sayfasına");
                return RedirectToAction("Login", "Account");
            }
            
            // Şifre alanlarının doğruluğunu kontrol et
            if (string.IsNullOrEmpty(model.NewPassword))
            {
                ModelState.AddModelError("NewPassword", "Yeni şifre alanı zorunludur.");
                return await RedirectToProfilePage(model.Id ?? "", "Yeni şifre alanı zorunludur.");
            }
            
            if (string.IsNullOrEmpty(model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "Şifre tekrar alanı zorunludur.");
                return await RedirectToProfilePage(model.Id ?? "", "Şifre tekrar alanı zorunludur.");
            }
            
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Şifreler eşleşmiyor.");
                return await RedirectToProfilePage(model.Id ?? "", "Şifreler eşleşmiyor.");
            }
            
            try
            {
                // Id veya email ile kullanıcıyı bul
                var user = string.IsNullOrEmpty(model.Id) 
                    ? await _userManager.FindByEmailAsync(userEmail)
                    : await _userManager.FindByIdAsync(model.Id);
                
                if (user == null)
                {
                    ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                    return await RedirectToProfilePage("", "Kullanıcı bulunamadı.");
                }
                
                _logger.LogInformation($"Şifre değiştirme işlemi başlatılıyor - Kullanıcı: {user.Email}");
                
                // Önce token oluştur
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                _logger.LogInformation($"Reset token oluşturuldu, token uzunluğu: {token?.Length ?? 0}");
                
                // Şifreyi değiştir
                var resetPasswordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!resetPasswordResult.Succeeded)
                {
                    string errorMessages = string.Join(", ", resetPasswordResult.Errors.Select(e => e.Description));
                    _logger.LogError($"Şifre değiştirme hataları: {errorMessages}");
                    
                    foreach (var error in resetPasswordResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    
                    return await RedirectToProfilePage(user.Id, $"Şifre değiştirme başarısız: {errorMessages}");
                }
                
                _logger.LogInformation($"Kullanıcı {user.Email} şifresini başarıyla değiştirdi");
                TempData["SuccessMessage"] = "Şifreniz başarıyla güncellenmiştir.";
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Şifre değiştirme işlemi sırasında hata: {ex.Message}, StackTrace: {ex.StackTrace}");
                ModelState.AddModelError("", "Şifre değiştirme sırasında bir hata oluştu: " + ex.Message);
                return await RedirectToProfilePage(model.Id ?? "", $"Şifre değiştirme sırasında hata: {ex.Message}");
            }
        }
        
        // Yardımcı metod: Hata mesajı ile profil sayfasına yönlendir
        private async Task<IActionResult> RedirectToProfilePage(string userId, string errorMessage = null)
        {
            try
            {
                string userEmail = HttpContext.Session.GetString("UserEmail");
                ApplicationUser user;
                
                // Kullanıcıyı Id veya Email ile bul
                if (!string.IsNullOrEmpty(userId))
                {
                    user = await _userManager.FindByIdAsync(userId);
                }
                else
                {
                    user = await _userManager.FindByEmailAsync(userEmail);
                }
                
                if (user == null)
                {
                    // Son çare olarak session'daki email'i kullan
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        user = await _userManager.FindByEmailAsync(userEmail);
                    }
                    
                    if (user == null)
                    {
                        TempData["ErrorMessage"] = "Kullanıcı bilgileri bulunamadı. Lütfen tekrar giriş yapın.";
                        return RedirectToAction("Login", "Account");
                    }
                }
                
                // Modeli hazırla
                var model = new ProfileViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    ProfileImageUrl = user.ProfileImageUrl
                };
                
                // Hata mesajı varsa ekle
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    TempData["ErrorMessage"] = errorMessage;
                }
                
                // ViewData'yı güncelle
                string profileImageUrl = user.ProfileImageUrl;
                string sessionProfileImage = HttpContext.Session.GetString("UserProfileImage");
                if (!string.IsNullOrEmpty(sessionProfileImage))
                {
                    profileImageUrl = sessionProfileImage;
                }
                
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = user.Email;
                ViewData["UserProfileImage"] = profileImageUrl;
                
                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"RedirectToProfilePage metodu hata: {ex.Message}");
                TempData["ErrorMessage"] = "Bir hata oluştu, lütfen tekrar deneyin.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        // Hesap silme işlemi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi hesap silme işlemine");
                return RedirectToAction("Login", "Account");
            }

            try 
            {
                // Kullanıcıyı veritabanından bulalım
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null) 
                {
                    _logger.LogWarning($"Hesap silme işlemi sırasında kullanıcı bulunamadı: {userEmail}");
                    TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }

                // Kullanıcıya ait tüm projeleri bul
                var userProjects = _dbContext.Projects.Where(p => p.UserId == user.Id).ToList();
                
                // Her bir proje için ilişkili görevleri sil
                foreach (var project in userProjects)
                {
                    var projectTasks = _dbContext.Tasks.Where(t => t.ProjectId == project.Id).ToList();
                    _dbContext.Tasks.RemoveRange(projectTasks);
                    _logger.LogInformation($"Proje {project.Id} için {projectTasks.Count} görev silindi");
                }
                
                // Projeleri sil
                _dbContext.Projects.RemoveRange(userProjects);
                _logger.LogInformation($"Kullanıcı {user.Id} için {userProjects.Count} proje silindi");
                
                // Veritabanındaki değişiklikleri kaydet
                await _dbContext.SaveChangesAsync();
                
                // Kullanıcıyı sil
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"Kullanıcı hesabı başarıyla silindi: {userEmail}");
                    
                    // Session'ı temizle
                    HttpContext.Session.Clear();
                    
                    // Kullanıcıyı çıkış yap sayfasına yönlendir
                    return RedirectToAction("SimpleLogout", "Account");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Kullanıcı hesabı silinirken hata oluştu: {errors}");
                    TempData["ErrorMessage"] = "Hesabınız silinirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                    return RedirectToAction("Index", "Profile");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Hesap silme işlemi sırasında hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Hesap silme işlemi sırasında bir hata oluştu.";
                return RedirectToAction("Index", "Profile");
            }
        }

        // İki Faktörlü Doğrulama ayarlarını güncelleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTwoFactorAuth(bool enableTwoFactor)
        {
            // Session kontrolü
            string isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            string userEmail = HttpContext.Session.GetString("UserEmail");
            
            if (string.IsNullOrEmpty(isAuthenticated) || isAuthenticated != "true")
            {
                _logger.LogWarning("Yetkisiz erişim denemesi 2FA ayarlarına");
                return RedirectToAction("Login", "Account");
            }

            try 
            {
                // Kullanıcıyı veritabanından bulalım
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null) 
                {
                    _logger.LogWarning($"2FA ayarı değiştirilirken kullanıcı bulunamadı: {userEmail}");
                    TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                    return RedirectToAction("Index", "Dashboard");
                }

                // 2FA ayarını güncelle
                user.TwoFactorEnabled = enableTwoFactor;
                
                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    _logger.LogInformation($"Kullanıcı {userEmail} için 2FA ayarı güncellendi: {enableTwoFactor}");
                    
                    if (enableTwoFactor)
                    {
                        TempData["SuccessMessage"] = "İki faktörlü doğrulama başarıyla etkinleştirildi.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "İki faktörlü doğrulama devre dışı bırakıldı.";
                    }
                }
                else
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    _logger.LogError($"2FA ayarı güncellenirken hata oluştu: {errors}");
                    TempData["ErrorMessage"] = "İki faktörlü doğrulama ayarları güncellenirken bir hata oluştu.";
                }
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"2FA ayarları güncellenirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "İki faktörlü doğrulama ayarları güncellenirken bir hata oluştu.";
                return RedirectToAction("Index");
            }
        }
    }
} 