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
    public class PlannerService : IPlannerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlannerService> _logger;

        public PlannerService(ApplicationDbContext context, ILogger<PlannerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PlannerTask>> GetTasksAsync(string userId)
        {
            try
            {
                _logger.LogInformation("GetTasksAsync: Kullanıcı görevleri alınıyor - UserId: {UserId}", userId);
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetTasksAsync: UserId boş veya null");
                    return new List<PlannerTask>();
                }
                
                try
                {
                    var tasks = await _context.PlannerTasks
                        .Where(t => t.UserId == userId)
                        .OrderBy(t => t.ParentTaskId)
                        .ThenBy(t => t.OrderIndex)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    _logger.LogInformation("GetTasksAsync: {Count} adet görev alındı", tasks.Count);
                    return tasks;
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "GetTasksAsync: Veritabanı erişimi sırasında hata: {ErrorMessage}", dbEx.Message);
                    if (dbEx.InnerException != null)
                    {
                        _logger.LogError("GetTasksAsync: Veritabanı iç hata: {InnerErrorMessage}", dbEx.InnerException.Message);
                    }
                    throw new Exception($"Veritabanından görevler alınırken hata: {dbEx.Message}", dbEx);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTasksAsync: Genel hata - UserId: {UserId}, Hata: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task<PlannerTask> GetTaskByIdAsync(int taskId)
        {
            try
            {
                return await _context.PlannerTasks
                    .Where(t => t.Id == taskId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving task with ID {TaskId}", taskId);
                throw;
            }
        }

        public async Task SaveTaskAsync(PlannerTask task)
        {
            try
            {
                task.CreatedAt = DateTime.UtcNow;
                
                // User referansını null olarak ayarla
                task.User = null;
                
                _context.PlannerTasks.Add(task);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving task {TaskName}", task.Name);
                throw;
            }
        }

        public async Task UpdateTaskAsync(PlannerTask task)
        {
            try
            {
                task.UpdatedAt = DateTime.UtcNow;
                
                // User referansını null olarak ayarla
                task.User = null;
                
                _context.PlannerTasks.Update(task);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task with ID {TaskId}", task.Id);
                throw;
            }
        }

        public async Task DeleteTaskAsync(int taskId)
        {
            try
            {
                var task = await _context.PlannerTasks.FindAsync(taskId);
                if (task != null)
                {
                    _context.PlannerTasks.Remove(task);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task with ID {TaskId}", taskId);
                throw;
            }
        }

        public async Task<List<PlannerTask>> GetChildTasksAsync(int parentTaskId)
        {
            try
            {
                return await _context.PlannerTasks
                    .Where(t => t.ParentTaskId == parentTaskId)
                    .OrderBy(t => t.OrderIndex)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving child tasks for parent ID {ParentTaskId}", parentTaskId);
                throw;
            }
        }

        public async Task<List<PlannerTask>> GetRootTasksAsync(string userId)
        {
            try
            {
                return await _context.PlannerTasks
                    .Where(t => t.UserId == userId && t.ParentTaskId == null)
                    .OrderBy(t => t.OrderIndex)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving root tasks for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<PlannerTask>> GetTasksByProjectIdAsync(int projectId)
        {
            try
            {
                return await _context.PlannerTasks
                    .Where(t => t.ProjectId == projectId)
                    .OrderBy(t => t.ParentTaskId)
                    .ThenBy(t => t.OrderIndex)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task DeleteTaskWithChildrenAsync(int taskId)
        {
            try
            {
                // Transaction kullan
                await using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    // Alt görevleri recursive olarak sil
                    await DeleteChildTasksRecursiveAsync(taskId);
                    
                    // Ana görevi sil
                    var task = await _context.PlannerTasks.FindAsync(taskId);
                    if (task != null)
                    {
                        _context.PlannerTasks.Remove(task);
                    }
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in transaction while deleting task with ID {TaskId}", taskId);
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task with children for ID {TaskId}", taskId);
                throw;
            }
        }

        private async Task DeleteChildTasksRecursiveAsync(int parentTaskId)
        {
            // Alt görevleri al
            var childTasks = await _context.PlannerTasks
                .Where(t => t.ParentTaskId == parentTaskId)
                .ToListAsync();
                
            // Her bir alt görev için recursion yap
            foreach (var childTask in childTasks)
            {
                // Önce bu görevin alt görevlerini sil
                await DeleteChildTasksRecursiveAsync(childTask.Id);
                
                // Sonra görevin kendisini sil
                _context.PlannerTasks.Remove(childTask);
            }
        }

        public async Task ReorderTasksAsync(int parentTaskId, List<int> taskIds)
        {
            try
            {
                // Transaction kullan
                await using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    for (int i = 0; i < taskIds.Count; i++)
                    {
                        var taskId = taskIds[i];
                        var task = await _context.PlannerTasks.FindAsync(taskId);
                        
                        if (task != null)
                        {
                            task.OrderIndex = i;
                            _context.PlannerTasks.Update(task);
                        }
                    }
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in transaction while reordering tasks for parent ID {ParentTaskId}", parentTaskId);
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering tasks for parent ID {ParentTaskId}", parentTaskId);
                throw;
            }
        }

        // Diagnostik metodlar
        public ApplicationDbContext GetDbContext()
        {
            return _context;
        }
        
        public async Task<int> GetTaskCountAsync()
        {
            try
            {
                return await _context.PlannerTasks.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTaskCountAsync: Görev sayısı alınırken hata oluştu");
                throw;
            }
        }
    }
} 