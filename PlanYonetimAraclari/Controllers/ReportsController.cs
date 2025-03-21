using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Services;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskStatus = PlanYonetimAraclari.Models.TaskStatus;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ILogger<ReportsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ApplicationDbContext _context;

        public ReportsController(
            ILogger<ReportsController> logger,
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            ApplicationDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _projectService = projectService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Giriş yapmış bir kullanıcı olup olmadığını kontrol et
                if (User.Identity == null || !User.Identity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt to reports");
                    return RedirectToAction("Login", "Account");
                }

                string userEmail = HttpContext.Session.GetString("UserEmail");
                
                _logger.LogInformation($"Raporlar sayfası açılıyor - Kullanıcı: {userEmail}");
                
                // Kullanıcı bilgilerini modele yükle
                var user = await _userManager.FindByEmailAsync(userEmail);
                
                if (user == null)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userEmail}");
                    TempData["ErrorMessage"] = "Oturum bilgilerinizde bir hata oluştu. Lütfen tekrar giriş yapın.";
                    return RedirectToAction("Login", "Account");
                }

                // Kullanıcının projelerini getir
                var projects = await _projectService.GetUserProjectsAsync(user.Id);
                
                // Proje tarihlerini yerel saate çevir
                foreach (var project in projects)
                {
                    project.CreatedDate = project.CreatedDate;
                    if (project.LastUpdatedDate.HasValue)
                        project.LastUpdatedDate = project.LastUpdatedDate;
                    project.StartDate = project.StartDate;
                    project.EndDate = project.EndDate;
                    if (project.DueDate.HasValue)
                        project.DueDate = project.DueDate;
                }

                // Proje durumlarına göre istatistikler
                var projectStats = new Dictionary<string, int>
                {
                    { "Planning", projects.Count(p => p.Status == ProjectStatus.Planning) },
                    { "InProgress", projects.Count(p => p.Status == ProjectStatus.InProgress) },
                    { "Completed", projects.Count(p => p.Status == ProjectStatus.Completed) },
                    { "OnHold", projects.Count(p => p.Status == ProjectStatus.OnHold) },
                    { "Cancelled", projects.Count(p => p.Status == ProjectStatus.Cancelled) }
                };

                // Tüm görevleri çek
                var allTasks = new List<TaskModel>();
                foreach (var p in projects)
                {
                    var tasks = await _projectService.GetProjectTasksAsync(p.Id);
                    
                    // Görev tarihlerini yerel saate çevir
                    foreach (var task in tasks)
                    {
                        task.CreatedDate = task.CreatedDate;
                        if (task.LastUpdatedDate.HasValue)
                            task.LastUpdatedDate = task.LastUpdatedDate;
                        if (task.DueDate.HasValue)
                            task.DueDate = task.DueDate;
                    }
                    
                    allTasks.AddRange(tasks);
                }

                // Görev durumlarına göre istatistikler
                var taskStats = new Dictionary<string, int>
                {
                    { "Todo", allTasks.Count(t => t.Status == TaskStatus.Todo) },
                    { "InProgress", allTasks.Count(t => t.Status == TaskStatus.InProgress) },
                    { "Done", allTasks.Count(t => t.Status == TaskStatus.Done) }
                };

                // Görev önceliklerine göre istatistikler
                var priorityStats = new Dictionary<string, int>
                {
                    { "Low", allTasks.Count(t => t.Priority == TaskPriority.Low) },
                    { "Medium", allTasks.Count(t => t.Priority == TaskPriority.Medium) },
                    { "High", allTasks.Count(t => t.Priority == TaskPriority.High) },
                    { "Urgent", allTasks.Count(t => t.Priority == TaskPriority.Urgent) }
                };

                // Son tamamlanan görevler (en yeni en üstte)
                var recentCompletedTasks = allTasks
                    .Where(t => t.Status == TaskStatus.Done)
                    .OrderByDescending(t => t.LastUpdatedDate ?? t.CreatedDate)
                    .Take(20)
                    .Select(t => {
                        // Tarihleri düzeltmeye gerek yok, AddHours view tarafında yapılacak
                        return t;
                    })
                    .ToList();

                // Son eklenen görevler (en yeni en üstte)
                var recentCreatedTasks = allTasks
                    .OrderByDescending(t => t.CreatedDate)
                    .Take(20)
                    .Select(t => {
                        // Tarihleri düzeltmeye gerek yok, AddHours view tarafında yapılacak
                        return t;
                    })
                    .ToList();

                // Rapor ViewModel oluştur
                var reportsViewModel = new ReportsViewModel
                {
                    Projects = projects,
                    Tasks = allTasks,
                    ProjectStats = projectStats,
                    TaskStats = taskStats,
                    PriorityStats = priorityStats,
                    RecentCompletedTasks = recentCompletedTasks,
                    RecentCreatedTasks = recentCreatedTasks
                };

                // Layout için gereken bilgileri ViewData'da sakla
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = userEmail;
                ViewData["UserProfileImage"] = user.ProfileImageUrl ?? "/images/default-profile.png";
                ViewBag.CurrentUserId = user.Id;
                
                return View(reportsViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Raporlar sayfası yüklenirken hata oluştu: {ex.Message}");
                TempData["ErrorMessage"] = "Raporlar sayfası yüklenirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return RedirectToAction("Index", "Dashboard");
            }
        }
    }
} 