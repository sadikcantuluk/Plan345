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
    public class CalendarService : ICalendarService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CalendarService> _logger;
        private readonly ActivityService _activityService;

        public CalendarService(ApplicationDbContext context, ILogger<CalendarService> logger, ActivityService activityService)
        {
            _context = context;
            _logger = logger;
            _activityService = activityService;
        }

        public async Task<List<CalendarNote>> GetUserUpcomingNotesAsync(string userId, int daysAhead = 7, int count = 5)
        {
            try
            {
                _logger.LogInformation($"Kullanıcının yaklaşan takvim notları alınıyor: {userId}");
                
                var today = DateTime.Today;
                var endDate = today.AddDays(daysAhead);
                
                return await _context.CalendarNotes
                    .Where(n => n.UserId == userId && 
                               !n.IsCompleted && 
                               n.NoteDate >= today && 
                               n.NoteDate <= endDate)
                    .OrderBy(n => n.NoteDate)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Kullanıcının yaklaşan takvim notları alınırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> MarkNoteAsCompletedAsync(int noteId, string userId)
        {
            try
            {
                _logger.LogInformation($"Takvim notu tamamlandı olarak işaretleniyor: {noteId} kullanıcı: {userId}");
                
                var note = await _context.CalendarNotes
                    .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
                
                if (note == null)
                {
                    _logger.LogWarning($"Tamamlanacak takvim notu bulunamadı: {noteId}");
                    return false;
                }
                
                note.IsCompleted = true;
                note.UpdatedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: userId,
                    type: ActivityType.CalendarNoteUpdated,
                    title: "Takvim notu tamamlandı",
                    description: note.Title,
                    relatedEntityId: note.Id,
                    relatedEntityType: "CalendarNote"
                );
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Takvim notu tamamlanırken hata oluştu: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> SnoozeNoteAsync(int noteId, string userId, DateTime newDate)
        {
            try
            {
                _logger.LogInformation($"Takvim notu erteleniyor: {noteId} kullanıcı: {userId}, yeni tarih: {newDate}");
                
                var note = await _context.CalendarNotes
                    .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
                
                if (note == null)
                {
                    _logger.LogWarning($"Ertelenecek takvim notu bulunamadı: {noteId}");
                    return false;
                }
                
                var oldDate = note.NoteDate;
                note.NoteDate = newDate;
                note.UpdatedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: userId,
                    type: ActivityType.CalendarNoteUpdated,
                    title: "Takvim notu ertelendi",
                    description: $"{note.Title} ({oldDate:dd.MM.yyyy} → {newDate:dd.MM.yyyy})",
                    relatedEntityId: note.Id,
                    relatedEntityType: "CalendarNote"
                );
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Takvim notu ertelenirken hata oluştu: {ex.Message}");
                throw;
            }
        }
    }
} 