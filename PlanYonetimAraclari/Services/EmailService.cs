using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PlanYonetimAraclari.Models;

namespace PlanYonetimAraclari.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string message);
        Task SendPasswordResetEmailAsync(string email, string callbackUrl);
        Task SendContactEmailAsync(string name, string email, string subject, string message);
        Task SendTwoFactorCodeAsync(string email, string code);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
            
            // Log yapılacaksa sadece bir kere ve debug modunda yapılsın 
            #if DEBUG
            // Gereksiz logları kaldırdık
            _logger.LogDebug("E-posta servisi başlatıldı.");
            #endif
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                _logger.LogInformation($"E-posta gönderimi başlatılıyor: {email}, Konu: {subject}");
                
                var mimeMessage = new MimeMessage();
                
                mimeMessage.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                mimeMessage.To.Add(new MailboxAddress("", email));
                mimeMessage.Subject = subject;
                
                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = message
                };
                
                mimeMessage.Body = bodyBuilder.ToMessageBody();
                
                _logger.LogInformation("SMTP istemcisi oluşturuluyor...");
                using (var client = new SmtpClient())
                {
                    // Daha ayrıntılı günlük kaydı için olay dinleyicileri ekleyin
                    client.MessageSent += (sender, args) => 
                    {
                        _logger.LogInformation($"Mesaj gönderildi: {args.Response}");
                    };
                    
                    // AlertAsync olayı bazı MailKit sürümlerinde bulunmayabilir
                    // client.AlertAsync += (sender, args) => 
                    // {
                    //     _logger.LogWarning($"SMTP Uyarı: {args.Message}");
                    // };
                    
                    // Güvenli bağlantı için SSL/TLS kullanıyoruz
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    
                    _logger.LogInformation($"SMTP sunucusuna bağlanılıyor: {_emailSettings.MailServer}:{_emailSettings.MailPort}");
                    await client.ConnectAsync(_emailSettings.MailServer, _emailSettings.MailPort, SecureSocketOptions.StartTls);
                    
                    _logger.LogInformation($"SMTP sunucusuna kimlik doğrulaması yapılıyor: {_emailSettings.UserName}");
                    await client.AuthenticateAsync(_emailSettings.UserName, _emailSettings.Password);
                    
                    _logger.LogInformation("E-posta gönderiliyor...");
                    await client.SendAsync(mimeMessage);
                    
                    _logger.LogInformation("SMTP sunucusundan bağlantı kesiliyor...");
                    await client.DisconnectAsync(true);
                    
                    _logger.LogInformation($"E-posta başarıyla gönderildi: {email}");
                }
            }
            catch (AuthenticationException ex)
            {
                _logger.LogError($"SMTP kimlik doğrulama hatası: {ex.Message}");
                _logger.LogError("Gmail için 'Uygulama Şifreleri' kullanıldığından emin olun ve şifrenin doğru olduğunu kontrol edin.");
                throw new Exception("SMTP kimlik doğrulama hatası: " + ex.Message, ex);
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError($"SMTP komut hatası: {ex.Message}, StatusCode: {ex.StatusCode}");
                throw new Exception("SMTP komut hatası: " + ex.Message, ex);
            }
            catch (SmtpProtocolException ex)
            {
                _logger.LogError($"SMTP protokol hatası: {ex.Message}");
                throw new Exception("SMTP protokol hatası: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"E-posta gönderirken genel hata oluştu: {ex.Message}");
                _logger.LogError($"Hata türü: {ex.GetType().Name}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"İç hata: {ex.InnerException.Message}, Türü: {ex.InnerException.GetType().Name}");
                }
                
                throw;
            }
        }
        
        public async Task SendPasswordResetEmailAsync(string email, string callbackUrl)
        {
            string subject = "Şifre Sıfırlama - Plan345";
            
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
                        .button {{ display: inline-block; background-color: #4f46e5; color: white !important; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ font-size: 12px; text-align: center; margin-top: 30px; color: #888; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <div class='logo'>Plan<span>345</span></div>
                        </div>
                        <div class='content'>
                            <h2>Şifre Sıfırlama Talebi</h2>
                            <p>Merhaba,</p>
                            <p>Plan345 hesabınız için şifre sıfırlama talebinde bulundunuz. Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:</p>
                            <div style='text-align: center;'>
                                <a class='button' href='{callbackUrl}' style='color: white !important; font-weight: bold; display: inline-block; background-color: #4f46e5; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin: 20px 0;'>Şifremi Sıfırla</a>
                            </div>
                            <p>Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                        </div>
                        <div class='footer'>
                            <p>Bu e-posta Plan345 Proje Yönetim Sistemi tarafından otomatik olarak gönderilmiştir.</p>
                            <p>&copy; 2023 Plan345. Tüm hakları saklıdır.</p>
                        </div>
                    </div>
                </body>
                </html>";
                
            await SendEmailAsync(email, subject, message);
        }
        
        public async Task SendContactEmailAsync(string name, string email, string subject, string message)
        {
            string emailSubject = $"İletişim Formu: {subject}";
            
            string emailBody = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}
                        .header {{ text-align: center; padding-bottom: 10px; border-bottom: 1px solid #eee; margin-bottom: 20px; }}
                        .logo {{ font-size: 24px; font-weight: bold; color: #333; }}
                        .logo span {{ color: #4f46e5; }}
                        .content {{ padding: 20px 0; }}
                        .field {{ margin-bottom: 15px; }}
                        .field strong {{ display: inline-block; width: 100px; color: #555; }}
                        .message-box {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-top: 15px; }}
                        .footer {{ font-size: 12px; text-align: center; margin-top: 30px; color: #888; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <div class='logo'>Plan<span>345</span></div>
                        </div>
                        <div class='content'>
                            <h2>Yeni İletişim Formu Mesajı</h2>
                            <p>Web sitesi iletişim formundan yeni bir mesaj alındı:</p>
                            
                            <div class='field'>
                                <strong>Ad Soyad:</strong> {name}
                            </div>
                            <div class='field'>
                                <strong>E-posta:</strong> {email}
                            </div>
                            <div class='field'>
                                <strong>Konu:</strong> {subject}
                            </div>
                            <div class='field'>
                                <strong>Mesaj:</strong>
                                <div class='message-box'>{message}</div>
                            </div>
                        </div>
                        <div class='footer'>
                            <p>Bu e-posta Plan345 Proje Yönetim Sistemi tarafından otomatik olarak gönderilmiştir.</p>
                            <p>&copy; 2023 Plan345. Tüm hakları saklıdır.</p>
                        </div>
                    </div>
                </body>
                </html>";
                
            // Kullanıcıya teşekkür e-postası
            string userSubject = "Plan345 - İletişim Talebiniz Alınmıştır";
            string userMessage = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}
                        .header {{ text-align: center; padding-bottom: 10px; border-bottom: 1px solid #eee; margin-bottom: 20px; }}
                        .logo {{ font-size: 24px; font-weight: bold; color: #333; }}
                        .logo span {{ color: #4f46e5; }}
                        .content {{ padding: 20px 0; }}
                        .footer {{ font-size: 12px; text-align: center; margin-top: 30px; color: #888; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <div class='logo'>Plan<span>345</span></div>
                        </div>
                        <div class='content'>
                            <h2>İletişim Talebiniz Alındı</h2>
                            <p>Merhaba {name},</p>
                            <p>İletişim formu aracılığıyla gönderdiğiniz mesaj başarıyla alınmıştır. En kısa sürede size geri dönüş yapacağız.</p>
                            <p>Mesajınızın konusu: <strong>{subject}</strong></p>
                            <p>Bizi tercih ettiğiniz için teşekkür ederiz.</p>
                        </div>
                        <div class='footer'>
                            <p>Bu e-posta Plan345 Proje Yönetim Sistemi tarafından otomatik olarak gönderilmiştir.</p>
                            <p>&copy; 2023 Plan345. Tüm hakları saklıdır.</p>
                        </div>
                    </div>
                </body>
                </html>";
            
            try
            {
                // İletişim formunu plan345destek@gmail.com adresine gönder
                _logger.LogInformation($"İletişim formu e-postası gönderiliyor: {_emailSettings.SenderEmail}");
                await SendEmailAsync(_emailSettings.SenderEmail, emailSubject, emailBody);
                
                // Kullanıcıya teşekkür e-postası gönder
                _logger.LogInformation($"Kullanıcıya teşekkür e-postası gönderiliyor: {email}");
                await SendEmailAsync(email, userSubject, userMessage);
                
                _logger.LogInformation($"İletişim formu e-postaları başarıyla gönderildi: {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"İletişim formu e-postası gönderilirken hata oluştu: {ex.Message}");
                _logger.LogError($"Hata detayları: {ex.ToString()}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"İç hata: {ex.InnerException.Message}");
                    _logger.LogError($"Stack trace: {ex.InnerException.StackTrace}");
                }
                
                throw new Exception($"İletişim formu e-postası gönderilirken hata: {ex.Message}", ex);
            }
        }

        // 2FA (İki Faktörlü Doğrulama) kodu gönderme metodu
        public async Task SendTwoFactorCodeAsync(string email, string code)
        {
            string subject = "Giriş Doğrulama Kodu - Plan345";
            
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
                        .code {{ font-size: 32px; font-weight: bold; letter-spacing: 5px; text-align: center; margin: 20px 0; padding: 10px; background-color: #f5f5f5; border-radius: 5px; color: #4f46e5; }}
                        .footer {{ font-size: 12px; text-align: center; margin-top: 30px; color: #888; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <div class='logo'>Plan<span>345</span></div>
                        </div>
                        <div class='content'>
                            <h2>Giriş Doğrulama Kodu</h2>
                            <p>Merhaba,</p>
                            <p>Plan345 hesabınıza giriş yapmak için aşağıdaki doğrulama kodunu kullanın:</p>
                            <div class='code'>{code}</div>
                            <p>Bu kod 5 dakika içinde geçerliliğini yitirecektir.</p>
                            <p>Eğer giriş denemesinde bulunmadıysanız, lütfen bu e-postayı görmezden gelin ve şifrenizi değiştirmeyi düşünün.</p>
                        </div>
                        <div class='footer'>
                            <p>Bu e-posta Plan345 Proje Yönetim Sistemi tarafından otomatik olarak gönderilmiştir.</p>
                            <p>&copy; @DateTime.Now.Year Plan345. Tüm hakları saklıdır.</p>
                        </div>
                    </div>
                </body>
                </html>";
                
            await SendEmailAsync(email, subject, message);
        }
    }
} 