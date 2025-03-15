using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using System.Diagnostics;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace PlanYonetimAraclari.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;

        public AccountController(
            ILogger<AccountController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
        }
        
        // DirectLogin endpoint for POST requests
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DirectLogin(string email, string password, bool rememberMe = false)
        {
            _logger.LogInformation($"DirectLogin POST metodu çağrıldı - Email: {email}, RememberMe: {rememberMe}");
            
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("DirectLogin - Email veya şifre boş gönderildi");
                TempData["ErrorMessage"] = "E-posta ve şifre alanları zorunludur.";
                return RedirectToAction("Login");
            }

            _logger.LogInformation($"DirectLogin denemesi: {email}");
            
            bool isValidLogin = false;
            string userName = "";
            string userRole = "User";
            
            try
            {
                // Kullanıcıyı e-posta adresine göre bul
                var user = await _userManager.FindByEmailAsync(email);
                
                if (user != null)
                {
                    _logger.LogInformation($"Kullanıcı veritabanında bulundu: {email}, şifre kontrolü yapılıyor");
                    
                    // Şifre kontrolü
                    var passwordValid = await _userManager.CheckPasswordAsync(user, password);
                    
                    if (passwordValid)
                    {
                        _logger.LogInformation($"Şifre doğrulama başarılı: {email}");
                        
                        // Kullanıcı rollerini kontrol et
                        var roles = await _userManager.GetRolesAsync(user);
                        
                        isValidLogin = true;
                        userName = $"{user.FirstName} {user.LastName}";
                        userRole = roles.Contains("Admin") ? "Admin" : "User";
                        
                        // Son giriş zamanını güncelle
                        user.LastLoginTime = DateTime.Now;
                        await _userManager.UpdateAsync(user);
                    }
                    else
                    {
                        _logger.LogWarning($"Şifre doğrulama başarısız: {email}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Kullanıcı veritabanında bulunamadı: {email}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı doğrulaması sırasında hata: {ex.Message}");
            }
            
            if (isValidLogin)
            {
                // Session ve TempData'da kullanıcı bilgilerini sakla
                HttpContext.Session.SetString("IsAuthenticated", "true");
                HttpContext.Session.SetString("UserEmail", email);
                HttpContext.Session.SetString("UserName", userName);
                HttpContext.Session.SetString("UserRole", userRole);
                
                // TempData da sakla (ilk girişte yararlı olabilir)
                TempData["UserEmail"] = email;
                TempData["UserName"] = userName;
                TempData["UserRole"] = userRole;
                
                _logger.LogInformation($"Başarılı giriş: {email}, Rol: {userRole}");
                
                // Rol bazlı yönlendirme
                if (userRole == "Admin")
                {
                    return RedirectToAction("Index", "Admin");
                }
                else
                {
                    return RedirectToAction("Index", "Dashboard");
                }
            }
            
            // Geçersiz giriş
            TempData["ErrorMessage"] = "Geçersiz e-posta veya şifre.";
            _logger.LogWarning($"Başarısız giriş denemesi: {email}");
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            _logger.LogInformation("Login GET metodu çağrıldı.");
            
            try
            {
                // Kullanıcı zaten giriş yapmışsa anasayfaya yönlendir
                if (User.Identity.IsAuthenticated)
                {
                    _logger.LogInformation("Kullanıcı zaten giriş yapmış, anasayfaya yönlendiriliyor.");
                    return RedirectToAction("Index", "Dashboard");
                }
                
                ViewData["ReturnUrl"] = returnUrl;
                _logger.LogInformation("Login sayfası gösteriliyor.");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login sayfası gösterilirken hata oluştu: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            
            // Form verilerinin doğru gelip gelmediğini kontrol edelim
            _logger.LogInformation($"Login denemesi - Email: {model.Email}, Password: {(string.IsNullOrEmpty(model.Password) ? "Boş" : "Dolu")}");
            
            if (ModelState.IsValid)
            {
                // Oturum açma süresi 5 saat (istekte bulunulmaması durumunda)
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, 
                    model.Password, 
                    model.RememberMe, 
                    lockoutOnFailure: true);
                
                if (result.Succeeded)
                {
                    // Giriş başarılı, son giriş zamanını güncelle
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    user.LastLoginTime = DateTime.Now;
                    await _userManager.UpdateAsync(user);
                    
                    _logger.LogInformation($"Kullanıcı başarıyla giriş yaptı: {model.Email}");

                    // Kullanıcının rolüne göre yönlendirme
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Dashboard");
                    }
                }
                
                if (result.IsLockedOut)
                {
                    _logger.LogWarning($"Hesap kilitlendi: {model.Email}");
                    ModelState.AddModelError("", "Çok fazla başarısız giriş denemesi. Hesabınız geçici olarak kilitlendi.");
                    return View(model);
                }
                
                ModelState.AddModelError("", "Geçersiz e-posta veya şifre");
                _logger.LogWarning($"Geçersiz giriş denemesi: {model.Email}");
            }
            else
            {
                // Model doğrulama hataları
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"Model doğrulama hatası: {error.ErrorMessage}");
                }
            }
            
            // Başarısız giriş durumunda modeli tekrar göster
            return View(model);
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            _logger.LogInformation("Register GET metodu çağrıldı.");
            
            try
            {
                // Kullanıcı zaten giriş yapmışsa anasayfaya yönlendir
                if (User.Identity.IsAuthenticated)
                {
                    _logger.LogInformation("Kullanıcı zaten giriş yapmış, anasayfaya yönlendiriliyor.");
                    return RedirectToAction("Index", "Home");
                }
                
                ViewData["ReturnUrl"] = returnUrl;
                _logger.LogInformation("Register sayfası gösteriliyor.");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Register sayfası gösterilirken hata oluştu: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = "")
        {
            _logger.LogInformation($"Register POST metodu başladı. Email: {model.Email}");
            
            ViewData["ReturnUrl"] = returnUrl;
            
            // Form verilerinin doğru gelip gelmediğini kontrol edelim
            _logger.LogInformation($"Kayıt denemesi - Email: {model.Email}, FirstName: {model.FirstName}, LastName: {model.LastName}, Şifre uzunluğu: {model.Password?.Length ?? 0}");
            
            // returnUrl değerini ModelState'ten kaldır, çünkü bu alan modelde yok
            ModelState.Remove("returnUrl");
            
            // ModelState'in durumunu logla
            _logger.LogInformation($"ModelState geçerli mi: {ModelState.IsValid}");
            
            foreach (var state in ModelState)
            {
                _logger.LogInformation($"Model property: {state.Key}, Hata sayısı: {state.Value.Errors.Count}");
                
                foreach (var error in state.Value.Errors)
                {
                    _logger.LogWarning($"Model doğrulama hatası [{state.Key}]: {error.ErrorMessage}");
                }
            }
            
            if (ModelState.IsValid)
            {
                _logger.LogInformation("ModelState geçerli, kullanıcı oluşturuluyor...");
                
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    CreatedDate = DateTime.Now,
                    ProfileImageUrl = "/images/profiles/default.jpg"
                };
                
                try
                {
                    _logger.LogInformation($"Yeni kullanıcı oluşturuluyor: {model.Email}");
                    var result = await _userManager.CreateAsync(user, model.Password);
                    
                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"Kullanıcı başarıyla oluşturuldu: {model.Email}, şimdi rol atanıyor");
                        
                        // Yeni kullanıcıya "User" rolünü ata
                        var roleResult = await _userManager.AddToRoleAsync(user, "User");
                        
                        if (roleResult.Succeeded)
                        {
                            _logger.LogInformation($"Yeni kullanıcı başarıyla kaydedildi ve User rolü atandı: {model.Email}");
                            
                            // Session'a bilgileri kaydet
                            HttpContext.Session.SetString("IsAuthenticated", "true");
                            HttpContext.Session.SetString("UserEmail", user.Email);
                            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                            HttpContext.Session.SetString("UserRole", "User");
                            
                            // TempData da sakla
                            TempData["RegisterSuccess"] = "Hesabınız başarıyla oluşturuldu. Lütfen giriş yapın.";
                            
                            _logger.LogInformation($"Kullanıcı başarıyla oluşturuldu, giriş sayfasına yönlendiriliyor: {user.Email}");
                            return RedirectToAction("Login", "Account");
                        }
                        else
                        {
                            // Rol atama başarısız olduysa hataları logla
                            foreach (var error in roleResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                                _logger.LogError($"Rol atama hatası: {error.Description}");
                            }
                        }
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                            _logger.LogError($"Kullanıcı oluşturma hatası: {error.Description}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Kayıt işlemi sırasında istisna oluştu: {ex.Message}");
                    ModelState.AddModelError("", "Kayıt işlemi sırasında bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
                }
            }
            else
            {
                // Model doğrulama hataları
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"Model doğrulama hatası: {error.ErrorMessage}");
                }
            }
            
            _logger.LogInformation("Kayıt başarısız, form yeniden gösteriliyor");
            // Kayıt başarısız olursa modeli tekrar göster
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Identity logout işlemini çalıştır
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Kullanıcı Identity ile oturumu kapatıldı.");
            
            // Session temizle
            HttpContext.Session.Clear();
            
            // Kullanıcıyı ana sayfaya yönlendir
            return RedirectToAction("Index", "Home");
        }
        
        // GET: Basit logout için
        [HttpGet]
        public IActionResult SimpleLogout()
        {
            // Session temizle
            HttpContext.Session.Clear();
            
            _logger.LogInformation("Kullanıcı basit session ile oturumu kapattı");
            
            // Kullanıcıyı ana sayfaya yönlendir
            return RedirectToAction("Index", "Home");
        }
        
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #region Şifre Sıfırlama İşlemleri

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                _logger.LogInformation($"Şifre sıfırlama talebi: {model.Email}");
                
                // Kullanıcıyı e-posta adresine göre bul
                var user = await _userManager.FindByEmailAsync(model.Email);
                
                string token = "";
                string callbackUrl = "";
                
                if (user == null)
                {
                    _logger.LogWarning($"Şifre sıfırlama talebi - Kullanıcı bulunamadı: {model.Email}");
                    
                    // DEV ORTAMI İÇİN: Test amaçlı olarak, kullanıcı olmasa da e-posta göndermeyi deneyelim
                    // Ancak bu durumda bile ResetPassword sayfasına yönlendirmeliyiz, Login'e değil
                    token = "TEST-TOKEN-123456789";
                    callbackUrl = Url.Action(
                        "ResetPassword", 
                        "Account",
                        new { code = token },
                        protocol: HttpContext.Request.Scheme);
                }
                else
                {
                    // Şifre sıfırlama token'ı oluştur
                    token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    
                    // Şifre sıfırlama bağlantısı oluştur
                    callbackUrl = Url.Action(
                        "ResetPassword", 
                        "Account",
                        new { userId = user.Id, code = token },
                        protocol: HttpContext.Request.Scheme);
                }
                
                try
                {
                    // E-posta göndermeyi dene (test amaçlı olarak kullanıcı olsa da olmasa da)
                    _logger.LogInformation($"E-posta gönderimi başlatılıyor: {model.Email}, Callback URL: {callbackUrl}");
                    
                    // E-posta gönder
                    await _emailService.SendPasswordResetEmailAsync(model.Email, callbackUrl);
                    _logger.LogInformation($"Şifre sıfırlama e-postası gönderildi: {model.Email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"E-posta gönderirken hata oluştu: {ex.Message}");
                    _logger.LogError($"Stack Trace: {ex.StackTrace}");
                    
                    if (ex.InnerException != null)
                    {
                        _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                    }
                    
                    // Hata mesajını göster
                    TempData["ErrorMessage"] = "E-posta gönderirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                }
                
                // Kullanıcıyı şifre sıfırlama bağlantısının gönderildiği konusunda bilgilendir
                TempData["SuccessMessage"] = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi. Lütfen e-postanızı kontrol edin.";
                return RedirectToAction("ForgotPasswordConfirmation");
            }
            
            // ModelState geçerli değilse, formu tekrar göster
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            // Şifre sıfırlama onay sayfasını göster
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? code = null)
        {
            if (code == null)
            {
                _logger.LogWarning("Şifre sıfırlama sayfası geçersiz token ile açılmaya çalışıldı");
                return BadRequest("Şifre sıfırlama için bir token gereklidir.");
            }
            
            var model = new ResetPasswordViewModel 
            { 
                Code = code 
            };
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            _logger.LogInformation($"Şifre sıfırlama işlemi başlatıldı: {model.Email}");
            
            // Kullanıcıyı e-posta adresine göre bul
            var user = await _userManager.FindByEmailAsync(model.Email);
            
            if (user == null)
            {
                _logger.LogWarning($"Şifre sıfırlama - Kullanıcı bulunamadı: {model.Email}");
                
                // Kullanıcıyı bilgilendirmek yerine, işlem başarılıymış gibi davran (güvenlik için)
                return RedirectToAction("ResetPasswordConfirmation");
            }
            
            // Şifreyi sıfırla
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            
            if (result.Succeeded)
            {
                _logger.LogInformation($"Şifre başarıyla sıfırlandı: {model.Email}");
                
                // Şifre sıfırlama başarılı e-postası gönder
                try
                {
                    string subject = "Şifre Sıfırlandı - Plan345";
                    string message = $@"
                        <html>
                        <head>
                            <style>
                                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}
                                .header {{ text-align: center; padding-bottom: 10px; border-bottom: 1px solid #eee; margin-bottom: 20px; }}
                                .logo {{ font-size: 24px; font-weight: bold; color: #333; }}
                                .logo span {{ color: #4f46e5; }}
                                .content {{ padding: 20px 0; }}
                                .button {{ display: inline-block; background-color: #4f46e5; color: white; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin: 20px 0; }}
                                .footer {{ font-size: 12px; text-align: center; margin-top: 30px; color: #888; }}
                            </style>
                        </head>
                        <body>
                            <div class='container'>
                                <div class='header'>
                                    <div class='logo'>Plan<span>345</span></div>
                                </div>
                                <div class='content'>
                                    <h2>Şifre Sıfırlama Başarılı</h2>
                                    <p>Merhaba,</p>
                                    <p>Plan345 hesabınızın şifresi başarıyla değiştirildi. Eğer bu değişikliği siz yapmadıysanız, lütfen hemen bizimle iletişime geçin.</p>
                                    <div style='text-align: center;'>
                                        <a class='button' href='{Url.Action("Login", "Account", null, Request.Scheme)}' style='color: white !important; font-weight: bold; display: inline-block; background-color: #4f46e5; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin: 20px 0;'>Giriş Yap</a>
                                    </div>
                                </div>
                                <div class='footer'>
                                    <p>Bu e-posta Plan345 Proje Yönetim Sistemi tarafından otomatik olarak gönderilmiştir.</p>
                                    <p>&copy; 2023 Plan345. Tüm hakları saklıdır.</p>
                                </div>
                            </div>
                        </body>
                        </html>";
                        
                    await _emailService.SendEmailAsync(model.Email, subject, message);
                    _logger.LogInformation($"Şifre sıfırlama onay e-postası gönderildi: {model.Email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Şifre sıfırlama onay e-postası gönderirken hata oluştu: {ex.Message}");
                    // E-posta gönderilemese bile, kullanıcı akışını bozmuyoruz
                }
                
                TempData["SuccessMessage"] = "Şifreniz başarıyla sıfırlandı. Yeni şifrenizle giriş yapabilirsiniz.";
                return RedirectToAction("ResetPasswordConfirmation");
            }
            
            // Hata durumunda, hataları göster
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
                _logger.LogError($"Şifre sıfırlama hatası: {error.Description}");
            }
            
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        #endregion
    }
} 