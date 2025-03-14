using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;

namespace PlanYonetimAraclari.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IEmailService _emailService;

    public HomeController(ILogger<HomeController> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactForm(ContactViewModel model)
    {
        // Form değerlerinin detaylı loglanması
        _logger.LogInformation($"Form değerleri - Name: {model.Name}, Email: {model.Email}, Subject: {model.Subject}, AcceptPrivacy: {model.AcceptPrivacy}");
        
        // Form verilerinin ham hali (debugging amaçlı)
        var acceptPrivacyValues = Request.Form["AcceptPrivacy"].ToString();
        _logger.LogInformation($"Ham form verileri - AcceptPrivacy değeri: '{acceptPrivacyValues}'");
        
        // KVKK checkbox'ı için değerleri düzeltme
        // Eğer form'da "false,true" varsa, true olarak kabul et
        if (acceptPrivacyValues.Contains("true"))
        {
            model.AcceptPrivacy = true;
            _logger.LogInformation("AcceptPrivacy değeri düzeltildi: true");
        }
        
        // Request header bilgileri (debugging amaçlı)
        _logger.LogInformation($"Content-Type: {Request.ContentType}");
        
        // AJAX isteği olup olmadığını belirle
        bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        
        if (ModelState.IsValid)
        {
            try
            {
                _logger.LogInformation($"İletişim formu geçerli, işleme başlıyor: {model.Name}, {model.Email}, {model.Subject}, AcceptPrivacy: {model.AcceptPrivacy}");
                
                // KVKK kontrolü - ek güvenlik
                if (!model.AcceptPrivacy)
                {
                    _logger.LogWarning("KVKK şartları kabul edilmemiş.");
                    ModelState.AddModelError("AcceptPrivacy", "KVKK şartlarını kabul etmelisiniz.");
                    
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "KVKK şartlarını kabul etmelisiniz." });
                    }
                    
                    TempData["ErrorMessage"] = "KVKK şartlarını kabul etmelisiniz.";
                    
                    // Form verilerini TempData'da sakla
                    TempData["ContactFormError"] = true;
                    TempData["ContactFormName"] = model.Name;
                    TempData["ContactFormEmail"] = model.Email;
                    TempData["ContactFormSubject"] = model.Subject;
                    TempData["ContactFormMessage"] = model.Message;
                    
                    return Redirect("/#contact");
                }
                
                // E-posta gönderme işlemi
                await _emailService.SendContactEmailAsync(
                    model.Name,
                    model.Email,
                    model.Subject,
                    model.Message
                );
                
                _logger.LogInformation($"İletişim formu başarıyla kaydedildi ve e-posta gönderildi: {model.Email}");
                
                if (isAjaxRequest)
                {
                    return Json(new { success = true, message = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız." });
                }
                
                // Kullanıcıya başarılı mesajı göster
                TempData["SuccessMessage"] = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız.";
                
                // Aynı sayfaya yönlendir, contact kısmına
                return Redirect("/#contact");
            }
            catch (Exception ex)
            {
                _logger.LogError($"İletişim formu gönderilirken hata oluştu: {ex.Message}");
                _logger.LogError($"Hata detayları: {ex.ToString()}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"İç hata: {ex.InnerException.Message}");
                }
                
                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = "Mesajınız gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
                }
                
                ModelState.AddModelError("", "Mesajınız gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
                
                // Hata durumunda form verilerini TempData'da sakla
                TempData["ContactFormError"] = true;
                TempData["ContactFormName"] = model.Name;
                TempData["ContactFormEmail"] = model.Email;
                TempData["ContactFormSubject"] = model.Subject;
                TempData["ContactFormMessage"] = model.Message;
                TempData["ErrorMessage"] = "Mesajınız gönderilirken bir hata oluştu. Lütfen formu kontrol edip tekrar deneyin.";
            }
        }
        else
        {
            _logger.LogWarning("İletişim formu doğrulama hatası");
            
            // Model durumunu detaylı bir şekilde loglayalım
            foreach (var key in ModelState.Keys)
            {
                var state = ModelState[key];
                if (state.Errors.Count > 0)
                {
                    _logger.LogWarning($"Alan: {key}, Hatalar: {string.Join(", ", state.Errors.Select(e => e.ErrorMessage))}");
                }
            }
            
            if (isAjaxRequest)
            {
                return Json(new { success = false, message = "Lütfen tüm alanları doğru şekilde doldurunuz.", errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                )});
            }
            
            // Doğrulama hatası durumunda form verilerini TempData'da sakla
            TempData["ContactFormError"] = true;
            TempData["ContactFormName"] = model.Name;
            TempData["ContactFormEmail"] = model.Email;
            TempData["ContactFormSubject"] = model.Subject;
            TempData["ContactFormMessage"] = model.Message;
            TempData["ErrorMessage"] = "Lütfen tüm alanları doğru şekilde doldurunuz.";
        }
        
        // Hata durumunda iletişim bölümüne dön
        return Redirect("/#contact");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
