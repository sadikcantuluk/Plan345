using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Extensions
{
    public class NotificationActionFilter : IAsyncActionFilter
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public NotificationActionFilter(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Sadece controller/view için çalışsın, API için çalışmasın
            if (context.Controller is Controller controller)
            {
                var user = await _userManager.GetUserAsync(controller.User);
                if (user != null)
                {
                    // Davet bildirimleri
                    var hasPendingInvitations = await _context.ProjectInvitations
                        .AnyAsync(i => i.InvitedEmail == user.Email && i.Status == InvitationStatus.Pending);
                    
                    // Yarınki görevler
                    var tomorrow = DateTime.Today.AddDays(1);
                    var hasTomorrowTasks = await _context.Tasks
                        .AnyAsync(t => t.AssignedMemberId == user.Id 
                               && t.DueDate.HasValue 
                               && t.DueDate.Value.Date == tomorrow.Date 
                               && t.Status != PlanYonetimAraclari.Models.TaskStatus.Done);
                               
                    // Yarınki takvim notları
                    var hasTomorrowNotes = await _context.CalendarNotes
                        .AnyAsync(n => n.UserId == user.Id 
                               && n.NoteDate.Date == tomorrow.Date 
                               && !n.IsCompleted);
                    
                    // Herhangi bir bildirim varsa
                    var hasAnyNotifications = hasPendingInvitations || hasTomorrowTasks || hasTomorrowNotes;
                    
                    controller.ViewData["HasPendingInvitations"] = hasPendingInvitations;
                    controller.ViewData["HasTomorrowReminders"] = hasTomorrowTasks || hasTomorrowNotes;
                    controller.ViewData["HasAnyNotifications"] = hasAnyNotifications;
                }
            }

            await next();
        }
    }
} 