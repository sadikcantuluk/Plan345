using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
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

        public AccountController(
            ILogger<AccountController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }
        
        // GET endpoint basitleştirilmiş giriş için
        [HttpGet("/Account/DirectLogin")]
        public async Task<IActionResult> DirectLogin(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "E-posta ve şifre alanları zorunludur.";
                return RedirectToAction("Login");
            }

            _logger.LogInformation($"DirectLogin denemesi: {email}");
            
            // Sabit test kullanıcıları - gerçek uygulamada asla böyle yapmayın!
            bool isValidLogin = false;
            string userName = "";
            string userRole = "User";
            
            // Admin kullanıcısı
            if (email == "admin@plan345.com" && password == "Admin123!")
            {
                isValidLogin = true;
                userName = "Admin Kullanıcı";
                userRole = "Admin";
            }
            // Test kullanıcıları
            else if (email == "user@plan345.com" && password == "User123!")
            {
                isValidLogin = true;
                userName = "Test Kullanıcısı";
            }
            else if (email == "demo@plan345.com" && password == "Demo123!")
            {
                isValidLogin = true;
                userName = "Demo Kullanıcı";
            }
            else
            {
                // Sabit kullanıcılarda bulunamadıysa veritabanından kontrol et
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
                            
                            // Kullanıcı rolünü kontrol et
                            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                            
                            isValidLogin = true;
                            userName = $"{user.FirstName} {user.LastName}";
                            userRole = isAdmin ? "Admin" : "User";
                            
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
        public IActionResult Login(string returnUrl = null)
        {
            // Kullanıcı zaten giriş yapmışsa anasayfaya yönlendir
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
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
        public IActionResult Register(string returnUrl = null)
        {
            // Kullanıcı zaten giriş yapmışsa anasayfaya yönlendir
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
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
                    CreatedDate = DateTime.Now
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
                            TempData["SuccessMessage"] = "Hesabınız başarıyla oluşturuldu. Lütfen giriş yapın.";
                            
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
    }
} 