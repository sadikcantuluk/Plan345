using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using PlanYonetimAraclari.Data;
using System.Diagnostics;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace PlanYonetimAraclari.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _dbContext;

        public AccountController(
            ILogger<AccountController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService,
            ApplicationDbContext dbContext)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _dbContext = dbContext;
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

            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                
                if (user != null)
                {
                    var passwordValid = await _userManager.CheckPasswordAsync(user, password);
                    
                    if (passwordValid)
                    {
                        // İki faktörlü doğrulama kontrolü - eğer etkinse, doğrulama kodu gönder
                        if (user.TwoFactorEnabled)
                        {
                            _logger.LogInformation($"Kullanıcı {email} için iki faktörlü doğrulama etkin");
                            
                            // Doğrulama kodu oluştur
                            string verificationCode = GenerateRandomCode();
                            
                            // Kodu veritabanına kaydet
                            await SaveVerificationCodeAsync(user.Id, verificationCode);
                            
                            // Kodu kullanıcının e-posta adresine gönder
                            try
                            {
                                // E-posta gönderme işlemini arka planda çalıştır
                                _ = Task.Run(async () => 
                                {
                                    try 
                                    {
                                        await _emailService.SendTwoFactorCodeAsync(email, verificationCode);
                                        _logger.LogInformation($"Doğrulama kodu {email} adresine arka planda gönderildi");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError($"Arka planda doğrulama kodu gönderilirken hata: {ex.Message}");
                                        _logger.LogError($"Stack Trace: {ex.StackTrace}");
                                    }
                                });
                                
                                _logger.LogInformation($"İki faktörlü doğrulama e-postası işlemi başlatıldı: {email}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Doğrulama kodu işlemi başlatılırken hata oluştu: {ex.Message}");
                                // Kod gönderiminde hata oluşsa bile kullanıcı akışını bozmuyoruz
                            }
                            
                            // TempData'da 2FA gerektiğini ve e-posta adresini sakla
                            TempData["RequiresTwoFactor"] = true;
                            TempData["UserEmail"] = email;
                            
                            // Doğrulama sayfasına yönlendir
                            return RedirectToAction("VerifyCode");
                        }

                        // Identity ile giriş yap
                        if (rememberMe)
                        {
                            // Kalıcı cookie oluştur (14 gün)
                            AuthenticationProperties props = new AuthenticationProperties
                            {
                                IsPersistent = true,
                                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
                            };
                            
                            await _signInManager.SignInAsync(user, props);
                        }
                        else
                        {
                            // Oturum cookie'si oluştur (tarayıcı kapanınca sona erer)
                            await _signInManager.SignInAsync(user, isPersistent: false);
                        }
                        
                        // Kullanıcı rollerini kontrol et
                        var roles = await _userManager.GetRolesAsync(user);
                        string userRole = roles.Contains("Admin") ? "Admin" : "User";
                        string userName = $"{user.FirstName} {user.LastName}";
                        
                        // Son giriş zamanını güncelle
                        user.LastLoginTime = DateTime.Now;
                        await _userManager.UpdateAsync(user);
                        
                        // Session bilgilerini ayarla
                        HttpContext.Session.SetString("IsAuthenticated", "true");
                        HttpContext.Session.SetString("UserEmail", email);
                        HttpContext.Session.SetString("UserName", userName);
                        HttpContext.Session.SetString("UserRole", userRole);
                        HttpContext.Session.SetString("UserProfileImage", user.ProfileImageUrl ?? "/images/profiles/default.png");
                        HttpContext.Session.SetString("CurrentUserId", user.Id);
                        
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
                }
                
                _logger.LogWarning($"Geçersiz giriş denemesi: {email}");
                TempData["ErrorMessage"] = "Geçersiz e-posta veya şifre.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı doğrulaması sırasında hata: {ex.Message}");
                TempData["ErrorMessage"] = "Giriş sırasında bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return RedirectToAction("Login");
            }
        }

        [HttpGet]
        [Route("Account/Login")]
        public IActionResult Login(string? returnUrl = null)
        {
            _logger.LogInformation("Login GET metodu çağrıldı.");
            
            try
            {
                // Kullanıcı kimlik kontrolünü daha güvenli yapalım
                // Request.Cookies koleksiyonundaki .AspNetCore.Identity.Application cookie'sini kontrol et
                bool isAuthCookiePresent = Request.Cookies.Keys.Any(k => k.StartsWith(".AspNetCore.Identity.Application"));
                
                if (User.Identity.IsAuthenticated && isAuthCookiePresent)
                {
                    _logger.LogInformation("Kullanıcı zaten giriş yapmış, anasayfaya yönlendiriliyor.");
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Dönüş URL'ini sakla
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
        [Route("Account/Login")]
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
                
                if (result.RequiresTwoFactor)
                {
                    _logger.LogInformation($"Kullanıcı {model.Email} için iki faktörlü doğrulama gerekiyor");
                    
                    // Kullanıcı bilgilerini al
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    
                    // Doğrulama kodu oluştur
                    string verificationCode = GenerateRandomCode();
                    
                    // Kodu veritabanına kaydet
                    await SaveVerificationCodeAsync(user.Id, verificationCode);
                    
                    // Kodu kullanıcının e-posta adresine gönder
                    try
                    {
                        // E-posta gönderme işlemini arka planda çalıştır
                        _ = Task.Run(async () => 
                        {
                            try 
                            {
                                await _emailService.SendTwoFactorCodeAsync(model.Email, verificationCode);
                                _logger.LogInformation($"Doğrulama kodu {model.Email} adresine arka planda gönderildi");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Arka planda doğrulama kodu gönderilirken hata: {ex.Message}");
                                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                            }
                        });
                        
                        _logger.LogInformation($"İki faktörlü doğrulama e-postası işlemi başlatıldı: {model.Email}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Doğrulama kodu işlemi başlatılırken hata oluştu: {ex.Message}");
                        // Kod gönderiminde hata oluşsa bile kullanıcı akışını bozmuyoruz
                    }
                    
                    // TempData'da 2FA gerektiğini ve e-posta adresini sakla
                    TempData["RequiresTwoFactor"] = true;
                    TempData["UserEmail"] = model.Email;
                    
                    // Doğrulama sayfasına yönlendir
                    return RedirectToAction("VerifyCode");
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
        [Route("Account/Register")]
        public IActionResult Register(string? returnUrl = null)
        {
            _logger.LogInformation("Register GET metodu çağrıldı.");
            
            try
            {
                // Kullanıcı kimlik kontrolünü daha güvenli yapalım
                bool isAuthCookiePresent = Request.Cookies.Keys.Any(k => k.StartsWith(".AspNetCore.Identity.Application"));
                
                if (User.Identity.IsAuthenticated && isAuthCookiePresent)
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
        [Route("Account/Register")]
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
                _logger.LogInformation("ModelState geçerli, kullanıcı kaydı için kontrol yapılıyor...");
                
                // E-posta adresi daha önce kayıtlı mı kontrol et
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning($"E-posta adresi zaten kayıtlı: {model.Email}");
                    ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanılıyor.");
                    return View(model);
                }
                
                try
                {
                    // Kayıt bilgilerini geçici olarak sakla
                    var registrationData = new Dictionary<string, string>
                    {
                        { "Email", model.Email },
                        { "FirstName", model.FirstName },
                        { "LastName", model.LastName },
                        { "Password", model.Password }
                    };
                    TempData["PendingRegistration"] = JsonConvert.SerializeObject(registrationData);
                    
                    // Doğrulama kodu oluştur
                    string verificationCode = GenerateRandomCode();
                    
                    // E-posta doğrulama kodunu TempData'da sakla
                    var pendingVerification = new EmailVerificationCode
                    {
                        Email = model.Email,
                        Code = verificationCode,
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddMinutes(15),
                        IsUsed = false,
                        Attempts = 0
                    };
                    
                    _dbContext.EmailVerificationCodes.Add(pendingVerification);
                    await _dbContext.SaveChangesAsync();
                    
                    // E-posta doğrulama kodunu gönder
                    try {
                        // E-posta gönderme işlemini arka planda çalıştır
                        _ = Task.Run(async () => 
                        {
                            try 
                            {
                                await _emailService.SendEmailVerificationCodeAsync(model.Email, verificationCode);
                                _logger.LogInformation($"E-posta doğrulama kodu arka planda gönderildi: {model.Email}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Arka planda e-posta doğrulama kodu gönderilirken hata: {ex.Message}");
                                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                            }
                        });
                        
                        _logger.LogInformation($"E-posta doğrulama işlemi başlatıldı: {model.Email}");
                    }
                    catch (Exception ex) {
                        _logger.LogError($"E-posta doğrulama kodu işlemi başlatılırken hata: {ex.Message}");
                        ModelState.AddModelError("", "E-posta gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
                        return View(model);
                    }
                    
                    // E-posta doğrulama token'ı oluştur ve geçici olarak TempData'da sakla
                    TempData["VerificationToken"] = Guid.NewGuid().ToString();
                    
                    // E-posta doğrulama sayfasına yönlendir
                    return RedirectToAction("VerifyEmail", new { 
                        email = model.Email, 
                        token = TempData["VerificationToken"].ToString() 
                    });
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
            
            // Auth cookie'lerini özellikle temizle
            foreach (var cookieName in HttpContext.Request.Cookies.Keys)
            {
                // Tüm auth ve session cookie'lerini temizle
                if (cookieName.StartsWith(".AspNetCore.") || 
                    cookieName.StartsWith("Plan345Auth") || 
                    cookieName.StartsWith(".AspNet."))
                {
                    Response.Cookies.Delete(cookieName);
                }
            }
            
            // Kullanıcıyı giriş sayfasına yönlendir - 302 statü kodu ile yönlendirme
            return RedirectToAction("Login", "Account");
        }
        
        // GET: Basit logout için
        [HttpGet]
        public async Task<IActionResult> SimpleLogout()
        {
            // Identity logout işlemini çalıştır
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Kullanıcı Identity ile oturumu kapatıldı.");
            
            // Session temizle
            HttpContext.Session.Clear();
            
            // Auth cookie'lerini özellikle temizle
            foreach (var cookieName in HttpContext.Request.Cookies.Keys)
            {
                // Tüm auth ve session cookie'lerini temizle
                if (cookieName.StartsWith(".AspNetCore.") || 
                    cookieName.StartsWith("Plan345Auth") || 
                    cookieName.StartsWith(".AspNet."))
                {
                    Response.Cookies.Delete(cookieName);
                }
            }
            
            _logger.LogInformation("Kullanıcı basit session ile oturumu kapattı");
            
            // Kullanıcıyı giriş sayfasına yönlendir
            return RedirectToAction("Login", "Account");
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
                    
                    // E-posta gönderme işlemini arka planda çalıştır
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            // E-posta gönder
                            await _emailService.SendPasswordResetEmailAsync(model.Email, callbackUrl);
                            _logger.LogInformation($"Şifre sıfırlama e-postası arka planda gönderildi: {model.Email}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Arka planda e-posta gönderilirken hata oluştu: {ex.Message}");
                            _logger.LogError($"Stack Trace: {ex.StackTrace}");
                            
                            if (ex.InnerException != null)
                            {
                                _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                                _logger.LogError($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
                            }
                        }
                    });
                    
                    // Kullanıcıyı şifre sıfırlama bağlantısının gönderildiği konusunda bilgilendir
                    TempData["SuccessMessage"] = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi. Lütfen e-postanızı kontrol edin.";
                    return RedirectToAction("ForgotPasswordConfirmation");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"E-posta gönderirken hata oluştu: {ex.Message}");
                    _logger.LogError($"Stack Trace: {ex.StackTrace}");
                    
                    if (ex.InnerException != null)
                    {
                        _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                        _logger.LogError($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
                    }
                    
                    // Hata mesajını göster - kullanıcıya daha detaylı bilgi ver
                    ModelState.AddModelError(string.Empty, "E-posta gönderirken bir hata oluştu. Lütfen sistem yöneticisiyle iletişime geçin.");
                    return View(model);
                }
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
                    string subject = "Şifre Sıfırlama Başarılı";
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
                        
                    // E-posta gönderme işlemini arka planda çalıştır
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(model.Email, subject, message);
                            _logger.LogInformation($"Şifre sıfırlama onay e-postası arka planda gönderildi: {model.Email}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Arka planda şifre sıfırlama onay e-postası gönderilirken hata oluştu: {ex.Message}");
                            _logger.LogError($"Stack Trace: {ex.StackTrace}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Şifre sıfırlama onay e-postası hazırlanırken hata oluştu: {ex.Message}");
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

        // Doğrulama kodu sayfası
        [HttpGet]
        public IActionResult VerifyCode()
        {
            // 2FA gerektiren bir giriş denemesi olup olmadığını kontrol et
            if (TempData["RequiresTwoFactor"] == null || !(bool)TempData["RequiresTwoFactor"] || TempData["UserEmail"] == null)
            {
                return RedirectToAction("Login");
            }
            
            // TempData'yı korumak için
            TempData.Keep("RequiresTwoFactor");
            TempData.Keep("UserEmail");
            
            var model = new TwoFactorCodeVerificationModel
            {
                Email = TempData["UserEmail"].ToString()
            };
            
            return View(model);
        }

        // Doğrulama kodu onaylama
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCode(TwoFactorCodeVerificationModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user == null)
                    {
                        _logger.LogWarning($"Doğrulama kodu kontrolü sırasında kullanıcı bulunamadı: {model.Email}");
                        ModelState.AddModelError("", "Geçersiz doğrulama kodu veya oturum zaman aşımına uğradı.");
                        return View(model);
                    }
                    
                    // Doğrulama kodunu kontrol et
                    bool isValid = await VerifyCodeAsync(user.Id, model.Code);
                    
                    if (isValid)
                    {
                        _logger.LogInformation($"Doğrulama kodu geçerli, kullanıcı giriş yapıyor: {model.Email}");
                        
                        // Kullanıcı rollerini kontrol et
                        var roles = await _userManager.GetRolesAsync(user);
                        string userRole = roles.Contains("Admin") ? "Admin" : "User";
                        string userName = $"{user.FirstName} {user.LastName}";
                        
                        // Son giriş zamanını güncelle
                        user.LastLoginTime = DateTime.Now;
                        await _userManager.UpdateAsync(user);
                        
                        // Açıkça SignInAsync ile oturum açtır
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        
                        // Session ve TempData'da kullanıcı bilgilerini sakla
                        HttpContext.Session.SetString("IsAuthenticated", "true");
                        HttpContext.Session.SetString("UserEmail", model.Email);
                        HttpContext.Session.SetString("UserName", userName);
                        HttpContext.Session.SetString("UserRole", userRole);
                        HttpContext.Session.SetString("UserProfileImage", user.ProfileImageUrl ?? "/images/profiles/default.png");
                        HttpContext.Session.SetString("CurrentUserId", user.Id);
                        
                        // TempData temizle
                        TempData.Remove("RequiresTwoFactor");
                        TempData.Remove("UserEmail");
                        
                        _logger.LogInformation($"2FA ile başarılı giriş: {model.Email}, Rol: {userRole}");
                        
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
                    else
                    {
                        _logger.LogWarning($"Geçersiz doğrulama kodu: {model.Email}");
                        ModelState.AddModelError("", "Geçersiz doğrulama kodu veya oturum zaman aşımına uğradı.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Doğrulama kodu kontrolü sırasında hata: {ex.Message}");
                    ModelState.AddModelError("", "Doğrulama sırasında bir hata oluştu.");
                    return View(model);
                }
            }
            
            return View(model);
        }

        // 6 haneli rastgele kod oluştur
        private string GenerateRandomCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // Doğrulama kodunu veritabanına kaydet
        private async Task SaveVerificationCodeAsync(string userId, string code)
        {
            var twoFactorCode = new TwoFactorCode
            {
                UserId = userId,
                Code = code,
                ExpiresAt = DateTime.Now.AddMinutes(5),
                IsUsed = false
            };
            
            _dbContext.TwoFactorCodes.Add(twoFactorCode);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"Kullanıcı {userId} için doğrulama kodu kaydedildi, son kullanma: {twoFactorCode.ExpiresAt}");
        }

        // Doğrulama kodunu kontrol et
        private async Task<bool> VerifyCodeAsync(string userId, string code)
        {
            var storedCode = await _dbContext.TwoFactorCodes
                .Where(c => c.UserId == userId && c.Code == code && !c.IsUsed && c.ExpiresAt > DateTime.Now)
                .OrderByDescending(c => c.ExpiresAt)
                .FirstOrDefaultAsync();
            
            if (storedCode != null)
            {
                // Kodu kullanıldı olarak işaretle
                storedCode.IsUsed = true;
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation($"Kullanıcı {userId} için doğrulama kodu onaylandı");
                return true;
            }
            
            _logger.LogWarning($"Kullanıcı {userId} için geçersiz veya süresi dolmuş doğrulama kodu");
            return false;
        }

        // E-posta doğrulama sayfası - GET
        [HttpGet]
        public IActionResult VerifyEmail(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("VerifyEmail - Email veya token boş gönderildi");
                return RedirectToAction("Login");
            }

            var model = new EmailVerificationViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        // E-posta doğrulama - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(EmailVerificationViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kayıt bilgilerini TempData'dan al
                if (TempData["PendingRegistration"] == null || TempData["VerificationToken"] == null)
                {
                    _logger.LogWarning($"VerifyEmail - Geçici kayıt bilgileri bulunamadı: {model.Email}");
                    TempData["ErrorMessage"] = "Oturum zaman aşımına uğradı. Lütfen tekrar kayıt olun.";
                    return RedirectToAction("Register");
                }
                
                // Token kontrol et
                if (model.Token != TempData["VerificationToken"].ToString())
                {
                    _logger.LogWarning($"VerifyEmail - Geçersiz token: {model.Email}");
                    TempData["ErrorMessage"] = "Geçersiz doğrulama bağlantısı. Lütfen tekrar kayıt olun.";
                    return RedirectToAction("Register");
                }

                // Doğrulama kodunu kontrol et
                var verificationCode = await _dbContext.EmailVerificationCodes
                    .Where(c => c.Email == model.Email && c.Code == model.Code && !c.IsUsed && c.ExpiresAt > DateTime.Now)
                    .OrderByDescending(c => c.ExpiresAt)
                    .FirstOrDefaultAsync();

                if (verificationCode != null)
                {
                    _logger.LogInformation($"E-posta doğrulama kodu geçerli: {model.Email}");

                    // Kodu kullanıldı olarak işaretle
                    verificationCode.IsUsed = true;
                    await _dbContext.SaveChangesAsync();
                    
                    // Geçici kayıt bilgilerini al ve deserialize et
                    var pendingRegistrationJson = TempData["PendingRegistration"].ToString();
                    var pendingRegistration = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(pendingRegistrationJson);
                    
                    // Kullanıcıyı oluştur
                    var user = new ApplicationUser
                    {
                        UserName = pendingRegistration["Email"],
                        Email = pendingRegistration["Email"],
                        FirstName = pendingRegistration["FirstName"],
                        LastName = pendingRegistration["LastName"],
                        CreatedDate = DateTime.Now,
                        ProfileImageUrl = "/images/profiles/default.jpg",
                        IsEmailVerified = true // E-posta doğrulanmış olarak kaydet
                    };
                    
                    // Kullanıcıyı veritabanına kaydet
                    _logger.LogInformation($"Yeni kullanıcı oluşturuluyor: {model.Email}");
                    var result = await _userManager.CreateAsync(user, pendingRegistration["Password"]);
                    
                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"Kullanıcı başarıyla oluşturuldu: {model.Email}, şimdi rol atanıyor");
                        
                        // Yeni kullanıcıya "User" rolünü ata
                        var roleResult = await _userManager.AddToRoleAsync(user, "User");
                        
                        if (roleResult.Succeeded)
                        {
                            _logger.LogInformation($"Yeni kullanıcı başarıyla kaydedildi ve User rolü atandı: {model.Email}");
                            
                            // Kullanıcıyı otomatik olarak giriş yaptır
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            
                            // Kullanıcı oturum bilgilerini ayarla
                            HttpContext.Session.SetString("IsAuthenticated", "true");
                            HttpContext.Session.SetString("UserEmail", user.Email);
                            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                            HttpContext.Session.SetString("UserRole", "User");
                            HttpContext.Session.SetString("UserProfileImage", user.ProfileImageUrl ?? "/images/profiles/default.png");
                            HttpContext.Session.SetString("CurrentUserId", user.Id);
                            
                            // Başarı mesajı
                            TempData["SuccessMessage"] = "Hesabınız başarıyla oluşturuldu ve e-posta adresiniz doğrulandı.";
                            
                            // Dashboard'a yönlendir
                            return RedirectToAction("Index", "Dashboard");
                        }
                        else
                        {
                            _logger.LogWarning($"Rol atama hatası: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                            ModelState.AddModelError("", "Hesap oluşturuldu ancak rol atanırken bir hata oluştu.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Kullanıcı oluşturma hatası: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                        ModelState.AddModelError("", "Hesap oluşturulurken bir hata oluştu.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Geçersiz e-posta doğrulama kodu: {model.Email}");
                    ModelState.AddModelError("", "Geçersiz doğrulama kodu veya oturum zaman aşımına uğradı.");
                }
            }

            // TempData'yı korumak için
            TempData.Keep("PendingRegistration");
            TempData.Keep("VerificationToken");
            
            return View(model);
        }

        // Yeniden doğrulama kodu gönderme - GET
        [HttpGet]
        public async Task<IActionResult> ResendVerification(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("ResendVerification - Email boş gönderildi");
                return RedirectToAction("Login");
            }

            // Geçici kayıt bilgilerini kontrol et
            if (TempData["PendingRegistration"] == null)
            {
                _logger.LogWarning($"ResendVerification - Geçici kayıt bilgileri bulunamadı: {email}");
                TempData["ErrorMessage"] = "Oturum zaman aşımına uğradı. Lütfen tekrar kayıt olun.";
                return RedirectToAction("Register");
            }

            // Daha önce oluşturduğumuz bu mail adresiyle ilgili kaydı bulalım
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null && existingUser.IsEmailVerified)
            {
                _logger.LogInformation($"ResendVerification - Kullanıcı e-postası zaten doğrulanmış: {email}");
                TempData["SuccessMessage"] = "E-posta adresiniz zaten doğrulanmış. Lütfen giriş yapın.";
                return RedirectToAction("Login");
            }

            // Yeni doğrulama kodu oluştur
            string verificationCode = GenerateRandomCode();
            
            // Veritabanına yeni bir doğrulama kodu ekle
            var verificationCodeEntity = new EmailVerificationCode
            {
                Email = email,
                Code = verificationCode,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(15),
                IsUsed = false,
                Attempts = 0
            };
            
            _dbContext.EmailVerificationCodes.Add(verificationCodeEntity);
            await _dbContext.SaveChangesAsync();
            
            // E-posta doğrulama kodunu gönder
            try {
                // E-posta gönderme işlemini arka planda çalıştır
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await _emailService.SendEmailVerificationCodeAsync(email, verificationCode);
                        _logger.LogInformation($"E-posta doğrulama kodu arka planda gönderildi: {email}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Arka planda e-posta doğrulama kodu gönderilirken hata: {ex.Message}");
                        _logger.LogError($"Stack Trace: {ex.StackTrace}");
                    }
                });
                
                _logger.LogInformation($"E-posta doğrulama işlemi başlatıldı: {email}");
            }
            catch (Exception ex) {
                _logger.LogError($"E-posta doğrulama kodu işlemi başlatılırken hata: {ex.Message}");
                TempData["ErrorMessage"] = "Doğrulama kodu gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return RedirectToAction("Register");
            }

            // E-posta doğrulama token'ı oluştur veya mevcut olanı koru
            if (TempData["VerificationToken"] == null)
            {
                TempData["VerificationToken"] = Guid.NewGuid().ToString();
            }
            
            // TempData'yı korumak için
            TempData.Keep("PendingRegistration");
            
            // Doğrulama sayfasına yönlendir
            return RedirectToAction("VerifyEmail", new { 
                email = email, 
                token = TempData["VerificationToken"].ToString() 
            });
        }
    }
} 