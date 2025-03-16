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
                    var hasPendingInvitations = await _context.ProjectInvitations
                        .AnyAsync(i => i.InvitedEmail == user.Email && i.Status == InvitationStatus.Pending);
                    
                    controller.ViewData["HasPendingInvitations"] = hasPendingInvitations;
                }
            }

            await next();
        }
    }
} 