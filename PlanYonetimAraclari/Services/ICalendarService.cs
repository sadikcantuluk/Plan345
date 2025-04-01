using PlanYonetimAraclari.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Services
{
    public interface ICalendarService
    {
        Task<List<CalendarNote>> GetUserUpcomingNotesAsync(string userId, int daysAhead = 7, int count = 5);
        Task<bool> MarkNoteAsCompletedAsync(int noteId, string userId);
        Task<bool> SnoozeNoteAsync(int noteId, string userId, DateTime newDate);
    }
} 