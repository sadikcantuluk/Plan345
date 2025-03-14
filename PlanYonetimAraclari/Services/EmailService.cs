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
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
            
            // E-posta ayarlarını başlangıçta kontrol et
            _logger.LogInformation("E-posta ayarları yüklenmiştir:");
            _logger.LogInformation($"Mail Server: {_emailSettings.MailServer}");
            _logger.LogInformation($"Mail Port: {_emailSettings.MailPort}");
            _logger.LogInformation($"Sender Name: {_emailSettings.SenderName}");
            _logger.LogInformation($"Sender Email: {_emailSettings.SenderEmail}");
            _logger.LogInformation($"Username: {_emailSettings.UserName}");
            _logger.LogInformation("Şifre yapılandırılmış: " + (!string.IsNullOrEmpty(_emailSettings.Password) ? "Evet" : "Hayır"));
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
    }
} 