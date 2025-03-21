using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;

namespace PlanYonetimAraclari.Services
{
    public class ActivityService
    {
        private readonly ApplicationDbContext _context;
        private static readonly int ActivityRetentionDays = 15;

        public ActivityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(string userId, ActivityType type, string title, string description = null, int? relatedEntityId = null, string relatedEntityType = null)
        {
            var activity = new ActivityLog
            {
                UserId = userId,
                Type = type,
                Title = title,
                Description = description,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                CreatedAt = DateTime.Now.AddHours(3)
            };

            _context.ActivityLogs.Add(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ActivityLog>> GetRecentActivitiesAsync(string userId, int count = 10)
        {
            return await _context.ActivityLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Include(a => a.User)
                .ToListAsync();
        }

        public async Task<List<ActivityLog>> GetUserAndTeamActivitiesAsync(string userId, int count = 20)
        {
            // Kullanıcının takım üyesi olduğu projeleri bul
            var userProjects = await _context.ProjectTeamMembers
                .Where(p => p.UserId == userId)
                .Select(p => p.ProjectId)
                .ToListAsync();

            // Kullanıcının kendi projeleri
            var ownedProjects = await _context.Projects
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            // Tüm ilgili proje ID'lerini birleştir
            var allProjectIds = userProjects.Concat(ownedProjects).Distinct().ToList();

            // İlgili aktiviteleri getir - filtrelenmiş sorgu
            var activityQuery = _context.ActivityLogs
                .Include(a => a.User)
                .Where(a => 
                    // Kendi etkinlikleri
                    a.UserId == userId ||
                    
                    // Projeye özgü etkinlikler: Sadece üyesi olduğun projelerin etkinlikleri
                    (a.RelatedEntityType == "Project" && 
                     allProjectIds.Contains(a.RelatedEntityId ?? -1)) ||
                    
                    // Görev ve notlara özgü etkinlikler: Sadece üyesi olduğun projelerdeki etkinlikler
                    ((a.RelatedEntityType == "Task" || a.RelatedEntityType == "Note" || a.RelatedEntityType == "CalendarNote") && 
                     allProjectIds.Contains(a.RelatedEntityId ?? -1))
                )
                .OrderByDescending(a => a.CreatedAt);

            // Takım üyesi eklenme/çıkarılma etkinliklerini özel olarak filtrele
            // Bu etkinliklerin sadece ilgili kullanıcıyı veya projenin sahibini ilgilendirmesi gerekir
            var teamEvents = await _context.ActivityLogs
                .Include(a => a.User)
                .Where(a => 
                    (a.Type == ActivityType.TeamMemberAdded || a.Type == ActivityType.TeamMemberRemoved) &&
                    (
                        // Kullanıcıyı doğrudan ilgilendiren takım olayları
                        (a.Description != null && a.Description.Contains(userId)) ||
                        
                        // Kullanıcının sahibi olduğu projelerdeki takım olayları
                        (a.RelatedEntityId.HasValue && ownedProjects.Contains(a.RelatedEntityId.Value))
                    )
                )
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // Normal etkinlikleri getir
            var normalActivities = await activityQuery.Take(count).ToListAsync();
            
            // Filtrelenmiş takım etkinliklerini ekle ve tekrarları kaldır
            var allActivities = normalActivities
                .Union(teamEvents, new ActivityIdComparer())
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToList();

            return allActivities;
        }

        /// <summary>
        /// 15 günden eski etkinlik kayıtlarını temizler
        /// </summary>
        public async Task<int> CleanupOldActivitiesAsync()
        {
            var cutoffDate = DateTime.Now.AddHours(3).AddDays(-ActivityRetentionDays);
            
            var oldActivities = await _context.ActivityLogs
                .Where(a => a.CreatedAt < cutoffDate)
                .ToListAsync();
            
            if (oldActivities.Any())
            {
                _context.ActivityLogs.RemoveRange(oldActivities);
                await _context.SaveChangesAsync();
            }
            
            return oldActivities.Count;
        }
        
        /// <summary>
        /// Etkinlik günlüklerinin kaç gün saklandığını döndürür
        /// </summary>
        public int GetActivityRetentionDays()
        {
            return ActivityRetentionDays;
        }

        /// <summary>
        /// Activity ID'ye göre karşılaştırma yapan sınıf
        /// </summary>
        private class ActivityIdComparer : IEqualityComparer<ActivityLog>
        {
            public bool Equals(ActivityLog x, ActivityLog y)
            {
                return x.Id == y.Id;
            }

            public int GetHashCode(ActivityLog obj)
            {
                return obj.Id.GetHashCode();
            }
        }
    }
} 