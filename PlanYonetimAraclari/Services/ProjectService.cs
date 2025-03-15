using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(ApplicationDbContext context, ILogger<ProjectService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ProjectModel>> GetUserProjectsAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı projeleri alınıyor: {userId}");
                return await _context.Projects
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı projeleri alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<ProjectModel> GetProjectByIdAsync(int projectId)
        {
            try
            {
                _logger.LogInformation($"Proje detayı alınıyor: {projectId}");
                return await _context.Projects.FindAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje detayı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<ProjectModel> CreateProjectAsync(ProjectModel project)
        {
            try
            {
                _logger.LogInformation($"Yeni proje oluşturuluyor: {project.Name} | Kullanıcı: {project.UserId}");
                _logger.LogInformation($"Proje detayları: Açıklama: {project.Description ?? "Boş"}, Durum: {project.Status}, DueDate: {project.DueDate}");
                
                // Temel doğrulama kontrolü
                if (string.IsNullOrEmpty(project.Name))
                {
                    _logger.LogWarning("Proje adı boş, kaydetme işlemi iptal ediliyor.");
                    throw new ArgumentException("Proje adı zorunludur.");
                }
                
                if (string.IsNullOrEmpty(project.UserId))
                {
                    _logger.LogWarning("UserId boş, kaydetme işlemi iptal ediliyor.");
                    throw new ArgumentException("Kullanıcı ID zorunludur.");
                }
                
                // Varsayılan durum atama
                if (project.Status == 0)
                {
                    project.Status = ProjectStatus.Planning;
                    _logger.LogInformation("Status 0 olduğu için varsayılan olarak Planning atandı");
                }
                
                _logger.LogInformation("Proje veritabanına eklenmeye çalışılıyor...");
                await _context.Projects.AddAsync(project);
                
                _logger.LogInformation("SaveChangesAsync çağrılıyor...");
                int affectedRows = await _context.SaveChangesAsync();
                
                _logger.LogInformation($"SaveChangesAsync tamamlandı. Etkilenen satır sayısı: {affectedRows}");
                
                if (affectedRows > 0)
                {
                    _logger.LogInformation($"Proje başarıyla oluşturuldu: ID={project.Id}, Adı={project.Name}, UserId={project.UserId}");
                    return project;
                }
                else
                {
                    _logger.LogWarning("Proje veritabanına eklendi ancak satır etkilenmedi. Veritabanı bağlantısı kontrol edilmeli.");
                    throw new Exception("Proje kaydedilirken hiç satır etkilenmedi. İşlem başarısız olabilir.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje oluşturulurken hata oluştu: {ex.Message}");
                _logger.LogError($"Hata Stack Trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                    _logger.LogError($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                throw;
            }
        }

        public async Task<ProjectModel> UpdateProjectAsync(ProjectModel project)
        {
            try
            {
                _logger.LogInformation($"Proje güncelleniyor: {project.Id} - {project.Name}");
                
                // Son güncelleme tarihini ayarla
                project.LastUpdatedDate = DateTime.Now;
                
                _context.Entry(project).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Proje başarıyla güncellendi: {project.Id} - {project.Name}");
                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje güncellenirken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteProjectAsync(int projectId)
        {
            try
            {
                _logger.LogInformation($"Proje siliniyor: {projectId}");
                
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                {
                    _logger.LogWarning($"Silinecek proje bulunamadı: {projectId}");
                    return false;
                }
                
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Proje başarıyla silindi: {projectId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje silinirken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<int> GetUserProjectsCountAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı proje sayısı alınıyor: {userId}");
                return await _context.Projects
                    .Where(p => p.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<int> GetUserActiveProjectsCountAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı aktif proje sayısı alınıyor: {userId}");
                return await _context.Projects
                    .Where(p => p.UserId == userId && 
                           (p.Status == ProjectStatus.Planning || 
                           p.Status == ProjectStatus.InProgress))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı aktif proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<int> GetUserCompletedProjectsCountAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı tamamlanmış proje sayısı alınıyor: {userId}");
                return await _context.Projects
                    .Where(p => p.UserId == userId && p.Status == ProjectStatus.Completed)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı tamamlanmış proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<int> GetUserPendingProjectsCountAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı beklemedeki proje sayısı alınıyor: {userId}");
                return await _context.Projects
                    .Where(p => p.UserId == userId && 
                           (p.Status == ProjectStatus.OnHold || 
                            p.Status == ProjectStatus.Cancelled))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı beklemedeki proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
    }
} 