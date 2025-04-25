using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using Microsoft.AspNetCore.Hosting;

namespace PlanYonetimAraclari.Services
{
    public class ProfileImageHelper
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProfileImageHelper> _logger;

        public ProfileImageHelper(
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager,
            ILogger<ProfileImageHelper> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Profil resminin durumunu kontrol eder ve gerekirse default resme yönlendirir
        /// </summary>
        /// <param name="user">Kullanıcı</param>
        /// <param name="httpContext">HTTP Context</param>
        /// <returns>Güncellenmiş profil resmi URL'i</returns>
        public async Task<string> CheckAndUpdateProfileImage(ApplicationUser user, HttpContext httpContext)
        {
            if (user == null)
            {
                return "/images/profiles/default.png";
            }

            try
            {
                // Profil resmi yolunu kontrol et
                if (!string.IsNullOrEmpty(user.ProfileImageUrl) && !user.ProfileImageUrl.Equals("/images/profiles/default.png"))
                {
                    // Resim dosyasının gerçekten var olup olmadığını kontrol et
                    string profileImagePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfileImageUrl.TrimStart('/'));
                    
                    if (!System.IO.File.Exists(profileImagePath))
                    {
                        _logger.LogWarning($"Kullanıcının profil resmi bulunamadı: {profileImagePath}, default resme yönlendiriliyor.");
                        
                        // Kullanıcı nesnesini güncelle
                        user.ProfileImageUrl = "/images/profiles/default.png";
                        
                        // Veritabanında güncelle
                        await _userManager.UpdateAsync(user);
                        
                        // Session'daki resim yolunu da güncelle
                        httpContext.Session.SetString("UserProfileImage", user.ProfileImageUrl);
                    }
                }
                
                return user.ProfileImageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profil resmi kontrol edilirken hata oluştu: {ex.Message}");
                return "/images/profiles/default.png";
            }
        }
    }
} 