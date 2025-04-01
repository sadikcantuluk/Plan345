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
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TaskService> _logger;
        private readonly ActivityService _activityService;

        public TaskService(ApplicationDbContext context, ILogger<TaskService> logger, ActivityService activityService)
        {
            _context = context;
            _logger = logger;
            _activityService = activityService;
        }

        public async Task<List<TaskModel>> GetUserAssignedTasksAsync(string userId, int count = 5)
        {
            try
            {
                _logger.LogInformation($"Kullanıcıya atanan görevler alınıyor: {userId}");
                
                return await _context.Tasks
                    .Where(t => t.AssignedMemberId == userId && t.Status != TaskStatus.Done)
                    .OrderBy(t => t.Priority)  // Öncelik sırasına göre sırala
                    .ThenBy(t => t.DueDate)    // Sonra bitiş tarihine göre
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcıya atanan görevler alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TaskModel>> GetUserUpcomingTasksAsync(string userId, int daysAhead = 7, int count = 5)
        {
            try
            {
                _logger.LogInformation($"Kullanıcının yaklaşan görevleri alınıyor: {userId}");
                
                var today = DateTime.Today;
                var endDate = today.AddDays(daysAhead);
                
                return await _context.Tasks
                    .Where(t => t.AssignedMemberId == userId && 
                               t.Status != TaskStatus.Done && 
                               t.DueDate.HasValue && 
                               t.DueDate.Value >= today && 
                               t.DueDate.Value <= endDate)
                    .OrderBy(t => t.DueDate)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcının yaklaşan görevleri alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> MarkTaskAsCompletedAsync(int taskId, string userId)
        {
            try
            {
                _logger.LogInformation($"Görev tamamlandı olarak işaretleniyor: {taskId} kullanıcı: {userId}");
                
                var task = await _context.Tasks.FindAsync(taskId);
                
                if (task == null)
                {
                    _logger.LogWarning($"Tamamlanacak görev bulunamadı: {taskId}");
                    return false;
                }
                
                // Kullanıcının bu görevi tamamlama yetkisi var mı kontrol et
                // (görevin atandığı kişi veya projenin sahibi olmalı)
                var project = await _context.Projects.FindAsync(task.ProjectId);
                bool isAuthorized = task.AssignedMemberId == userId || project?.UserId == userId;
                
                if (!isAuthorized)
                {
                    var isMember = await _context.ProjectTeamMembers
                        .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId);
                    
                    isAuthorized = isMember;
                }
                
                if (!isAuthorized)
                {
                    _logger.LogWarning($"Kullanıcının görevi tamamlama yetkisi yok: {userId}, görev: {taskId}");
                    return false;
                }
                
                task.Status = TaskStatus.Done;
                task.LastUpdatedDate = DateTime.Now;
                
                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: userId,
                    type: ActivityType.TaskCompleted,
                    title: "Görev tamamlandı",
                    description: task.Name,
                    relatedEntityId: task.ProjectId,
                    relatedEntityType: "Project"
                );
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görev tamamlanırken hata oluştu: {ex.Message}");
                throw;
            }
        }
    }
} 