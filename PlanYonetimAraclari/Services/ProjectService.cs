using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskStatus = PlanYonetimAraclari.Models.TaskStatus;

namespace PlanYonetimAraclari.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectService> _logger;
        private readonly ActivityService _activityService;

        public ProjectService(ApplicationDbContext context, ILogger<ProjectService> logger, ActivityService activityService)
        {
            _context = context;
            _logger = logger;
            _activityService = activityService;
        }

        public async Task<List<ProjectModel>> GetUserProjectsAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı projeleri alınıyor: {userId}");
                var ownedProjects = await _context.Projects
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync();

                var teamProjects = await _context.ProjectTeamMembers
                    .Where(m => m.UserId == userId)
                    .Include(m => m.Project)
                    .Select(m => m.Project)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync();

                return ownedProjects.Union(teamProjects).OrderByDescending(p => p.CreatedDate).ToList();
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
                return await _context.Projects
                    .Include(p => p.TeamMembers)
                        .ThenInclude(m => m.User)
                    .FirstOrDefaultAsync(p => p.Id == projectId);
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
                    
                    // Projeyi oluşturan kullanıcıyı otomatik olarak ekip üyesi (Manager rolünde) olarak ekle
                    var teamMember = new ProjectTeamMember
                    {
                        ProjectId = project.Id,
                        UserId = project.UserId,
                        Role = TeamMemberRole.Manager,
                        Status = "Accepted",
                        InvitedAt = DateTime.Now,
                        JoinedAt = DateTime.Now
                    };
                    
                    _logger.LogInformation($"Proje sahibi ekip üyesi olarak ekleniyor: ProjectId={project.Id}, UserId={project.UserId}, Role=Manager");
                    await _context.ProjectTeamMembers.AddAsync(teamMember);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Proje sahibi başarıyla ekip üyesi olarak eklendi");
                    
                    // Etkinlik kaydı oluştur
                    await _activityService.LogActivityAsync(
                        userId: project.UserId,
                        type: ActivityType.ProjectCreated,
                        title: "Yeni proje oluşturuldu",
                        description: project.Name,
                        relatedEntityId: project.Id,
                        relatedEntityType: "Project"
                    );
                    
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
                
                // Eski projeyi al (değişiklik sonrası karşılaştırma için)
                var oldProject = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == project.Id);
                
                // Son güncelleme tarihini ayarla
                project.LastUpdatedDate = DateTime.Now;
                
                _context.Entry(project).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Proje başarıyla güncellendi: {project.Id} - {project.Name}");
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: project.UserId,
                    type: ActivityType.ProjectUpdated,
                    title: "Proje güncellendi",
                    description: project.Name,
                    relatedEntityId: project.Id,
                    relatedEntityType: "Project"
                );
                
                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje güncellenirken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteProjectAsync(int projectId, bool forceDelete = false)
        {
            try
            {
                _logger.LogInformation($"Proje siliniyor: {projectId}, Force Delete: {forceDelete}");
                
                var project = await _context.Projects
                    .Include(p => p.TeamMembers)
                    .Include(p => p.Invitations)
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                    
                if (project == null)
                {
                    _logger.LogWarning($"Silinecek proje bulunamadı: {projectId}");
                    return false;
                }
                
                // Projenin adı ve kullanıcı ID'si silme sonrası etkinlik kaydı için
                var projectName = project.Name;
                var userId = project.UserId;
                
                // Projeye ait görevleri kontrol et
                var hasRelatedTasks = await _context.Tasks.AnyAsync(t => t.ProjectId == projectId);
                
                if (hasRelatedTasks && !forceDelete)
                {
                    _logger.LogWarning($"Projeye ait görevler olduğu için silme işlemi iptal edildi: {projectId}");
                    return false;
                }
                
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Önce projeye ait tüm takım üyelerini sil
                        if (project.TeamMembers != null && project.TeamMembers.Any())
                        {
                            _context.ProjectTeamMembers.RemoveRange(project.TeamMembers);
                            _logger.LogInformation($"Projeye ait {project.TeamMembers.Count} takım üyesi silindi: {projectId}");
                        }
                        
                        // Projeye ait tüm davetleri sil
                        if (project.Invitations != null && project.Invitations.Any())
                        {
                            _context.ProjectInvitations.RemoveRange(project.Invitations);
                            _logger.LogInformation($"Projeye ait {project.Invitations.Count} davetiye silindi: {projectId}");
                        }
                        
                        // Eğer forceDelete true ise veya görevler yoksa, görevleri sil
                        if (forceDelete && hasRelatedTasks)
                        {
                            // Önce projeye ait tüm görevleri sil
                            var projectTasks = await _context.Tasks.Where(t => t.ProjectId == projectId).ToListAsync();
                            _context.Tasks.RemoveRange(projectTasks);
                            _logger.LogInformation($"Projeye ait {projectTasks.Count} görev silindi: {projectId}");
                        }
                        
                        // Projeyi sil
                        _context.Projects.Remove(project);
                        
                        // Değişiklikleri kaydet
                        await _context.SaveChangesAsync();
                        
                        // Transaction'ı tamamla
                        await transaction.CommitAsync();
                        
                        _logger.LogInformation($"Proje başarıyla silindi: {projectId}");
                        
                        // Etkinlik kaydı oluştur
                        await _activityService.LogActivityAsync(
                            userId: userId,
                            type: ActivityType.ProjectDeleted,
                            title: "Proje silindi",
                            description: projectName,
                            relatedEntityId: null,
                            relatedEntityType: "Project"
                        );
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Hata durumunda transaction'ı geri al
                        await transaction.RollbackAsync();
                        _logger.LogError($"Proje silinirken transaction hatası oluştu: {ex.Message}");
                        throw;
                    }
                }
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
                
                // Kullanıcının sahip olduğu ve ekip üyesi olduğu projeleri tek sorguda al
                var projectIds = await _context.Projects
                    .Where(p => p.UserId == userId || p.TeamMembers.Any(m => m.UserId == userId))
                    .Select(p => p.Id)
                    .Distinct()
                    .CountAsync();

                return projectIds;
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
                
                // Planlama ve Devam Eden durumundaki projeleri say
                var projectIds = await _context.Projects
                    .Where(p => (p.UserId == userId || p.TeamMembers.Any(m => m.UserId == userId)) &&
                               (p.Status == ProjectStatus.Planning || p.Status == ProjectStatus.InProgress))
                    .Select(p => p.Id)
                    .Distinct()
                    .CountAsync();

                return projectIds;
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
                _logger.LogInformation($"Kullanıcı tamamlanan proje sayısı alınıyor: {userId}");
                
                // Tamamlanmış durumundaki projeleri say
                var projectIds = await _context.Projects
                    .Where(p => (p.UserId == userId || p.TeamMembers.Any(m => m.UserId == userId)) &&
                               p.Status == ProjectStatus.Completed)
                    .Select(p => p.Id)
                    .Distinct()
                    .CountAsync();

                return projectIds;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı tamamlanan proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<int> GetUserPendingProjectsCountAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcı beklemedeki proje sayısı alınıyor: {userId}");
                
                // Beklemede durumundaki projeleri say
                var projectIds = await _context.Projects
                    .Where(p => (p.UserId == userId || p.TeamMembers.Any(m => m.UserId == userId)) &&
                               p.Status == ProjectStatus.OnHold)
                    .Select(p => p.Id)
                    .Distinct()
                    .CountAsync();

                return projectIds;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcı beklemedeki proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<ProjectModel> GetProjectDetailsByIdAsync(int projectId)
        {
            try
            {
                _logger.LogInformation($"Proje detayları alınıyor: {projectId}");
                return await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje detayları alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<bool> UpdateProjectStatusAsync(int projectId, ProjectStatus newStatus)
        {
            try
            {
                _logger.LogInformation($"Proje durumu güncelleniyor: {projectId} - Yeni durum: {newStatus}");
                
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                {
                    _logger.LogWarning($"Güncellenecek proje bulunamadı: {projectId}");
                    return false;
                }
                
                project.Status = newStatus;
                project.LastUpdatedDate = DateTime.Now;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Proje durumu başarıyla güncellendi: {projectId} - Yeni durum: {newStatus}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje durumu güncellenirken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<List<TaskModel>> GetProjectTasksAsync(int projectId)
        {
            try
            {
                _logger.LogInformation($"Proje görevleri alınıyor: {projectId}");
                return await _context.Tasks
                    .Where(t => t.ProjectId == projectId)
                    .OrderBy(t => t.Status)
                    .ThenBy(t => t.Priority)
                    .ThenByDescending(t => t.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje görevleri alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<List<TaskModel>> GetProjectTasksByStatusAsync(int projectId, TaskStatus status)
        {
            try
            {
                _logger.LogInformation($"Proje görevleri duruma göre alınıyor: {projectId} - Durum: {status}");
                return await _context.Tasks
                    .Where(t => t.ProjectId == projectId && t.Status == status)
                    .OrderBy(t => t.Priority)
                    .ThenByDescending(t => t.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Proje görevleri duruma göre alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<TaskModel> CreateTaskAsync(TaskModel task)
        {
            try
            {
                _logger.LogInformation($"Yeni görev oluşturuluyor: {task.Name} | Proje ID: {task.ProjectId}");
                
                if (string.IsNullOrEmpty(task.Name))
                {
                    _logger.LogWarning("Görev adı boş, kaydetme işlemi iptal ediliyor.");
                    throw new ArgumentException("Görev adı zorunludur.");
                }
                
                task.CreatedDate = DateTime.Now;
                
                await _context.Tasks.AddAsync(task);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Görev başarıyla oluşturuldu: {task.Id} - {task.Name}");
                
                // Etkinlik kaydı oluştur - işlemi yapan kişinin ID'sini kullan
                string actorUserId = task.CreatedByUserId ?? _context.Projects.FirstOrDefault(p => p.Id == task.ProjectId)?.UserId ?? "Sistem";
                string assignedToName = "";
                
                // Görev atanan kişinin adını sadece atama varsa ekle
                if (!string.IsNullOrEmpty(task.AssignedMemberId) && task.AssignedMemberId != actorUserId)
                {
                    var assignedUser = await _context.Users.FindAsync(task.AssignedMemberId);
                    if (assignedUser != null)
                    {
                        assignedToName = $" ({assignedUser.FullName} kişisine atandı)";
                    }
                }
                
                await _activityService.LogActivityAsync(
                    userId: actorUserId,
                    type: ActivityType.TaskCreated,
                    title: "Yeni görev oluşturuldu",
                    description: task.Name + assignedToName,
                    relatedEntityId: task.ProjectId,
                    relatedEntityType: "Project"
                );
                
                return task;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev oluşturulurken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<TaskModel> UpdateTaskAsync(TaskModel task)
        {
            try
            {
                // Önceki görev durumunu kontrol etmek için veritabanından görevi al
                var originalTask = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == task.Id);
                bool isStatusChange = originalTask != null && originalTask.Status != task.Status;
                bool isAssignmentChange = originalTask != null && originalTask.AssignedMemberId != task.AssignedMemberId;
                
                _logger.LogInformation($"Görev güncelleniyor: {task.Id} - {task.Name}");
                
                task.LastUpdatedDate = DateTime.Now;
                
                _context.Entry(task).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Görev başarıyla güncellendi: {task.Id} - {task.Name}");
                
                var activityType = task.Status == TaskStatus.Done 
                    ? ActivityType.TaskCompleted 
                    : ActivityType.TaskUpdated;
                
                var activityTitle = task.Status == TaskStatus.Done
                    ? "Görev tamamlandı"
                    : "Görev güncellendi";
                
                // İşlemi yapan kullanıcının ID'sini kullan
                string actorUserId = task.UpdatedByUserId ?? task.CreatedByUserId ?? _context.Projects.FirstOrDefault(p => p.Id == task.ProjectId)?.UserId ?? "Sistem";
                string assignedToName = "";
                
                // Görev atanan kişinin adını sadece atama değişikliği varsa ekle
                if (isAssignmentChange && !string.IsNullOrEmpty(task.AssignedMemberId))
                {
                    var assignedUser = await _context.Users.FindAsync(task.AssignedMemberId);
                    if (assignedUser != null)
                    {
                        assignedToName = $" ({assignedUser.FullName} kişisine atandı)";
                    }
                }
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: actorUserId,
                    type: activityType,
                    title: activityTitle,
                    description: task.Name + assignedToName,
                    relatedEntityId: task.ProjectId,
                    relatedEntityType: "Project"
                );
                
                return task;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev güncellenirken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<bool> DeleteTaskAsync(int taskId)
        {
            try
            {
                _logger.LogInformation($"Görev siliniyor: {taskId}");
                
                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null)
                {
                    _logger.LogWarning($"Silinecek görev bulunamadı: {taskId}");
                    return false;
                }
                
                string taskName = task.Name;
                int projectId = task.ProjectId;
                
                // İşlemi yapan kullanıcının ID'sini kullan
                string actorUserId = task.UpdatedByUserId ?? task.CreatedByUserId ?? _context.Projects.FirstOrDefault(p => p.Id == task.ProjectId)?.UserId ?? "Sistem";
                
                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: actorUserId,
                    type: ActivityType.TaskDeleted,
                    title: "Görev silindi",
                    description: taskName,
                    relatedEntityId: projectId,
                    relatedEntityType: "Project"
                );
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev silinirken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<int> GetUserOwnedProjectsCountAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcının sahip olduğu proje sayısı alınıyor: {userId}");
                
                // Sadece kullanıcının sahip olduğu projeleri say (ekip üyesi olduğu projeleri sayma)
                var ownedProjectsCount = await _context.Projects
                    .CountAsync(p => p.UserId == userId);

                _logger.LogInformation($"Kullanıcının sahip olduğu proje sayısı: {ownedProjectsCount}");
                return ownedProjectsCount;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcının sahip olduğu proje sayısı alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }
        
        public async Task<bool> IsUserProjectMemberAsync(string userId, int projectId)
        {
            try
            {
                _logger.LogInformation($"Kullanıcının proje üyesi olup olmadığı kontrol ediliyor: UserId={userId}, ProjectId={projectId}");
                
                // Proje sahibi mi kontrol et
                var isOwner = await _context.Projects
                    .AnyAsync(p => p.Id == projectId && p.UserId == userId);
                
                if (isOwner)
                {
                    _logger.LogInformation($"Kullanıcı projenin sahibi: UserId={userId}, ProjectId={projectId}");
                    return true;
                }
                
                // Ekip üyesi mi kontrol et
                var isMember = await _context.ProjectTeamMembers
                    .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.Status == "Accepted");
                
                _logger.LogInformation($"Kullanıcının proje üyeliği: UserId={userId}, ProjectId={projectId}, IsMember={isMember}");
                return isMember;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcının proje üyeliği kontrol edilirken hata oluştu: {ex.Message}");
                throw;
            }
        }
    }
} 