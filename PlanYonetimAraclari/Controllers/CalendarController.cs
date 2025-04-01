using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using PlanYonetimAraclari.Services;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/calendar")]
    public class CalendarApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ActivityService _activityService;

        public CalendarApiController(ApplicationDbContext context, ActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
        }

        [HttpGet("view")]
        public IActionResult CalendarView()
        {
            return File("~/Views/Calendar/Index.cshtml", "text/html");
        }

        [HttpGet("notes")]
        public async Task<IActionResult> GetNotes([FromQuery] string start, [FromQuery] string end)
        {
            try
            {
                if (!DateTime.TryParse(start, out DateTime startDate) || !DateTime.TryParse(end, out DateTime endDate))
                {
                    return BadRequest(new { success = false, error = "Geçersiz tarih formatı" });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var notes = await _context.CalendarNotes
                    .Where(n => n.UserId == userId && n.NoteDate >= startDate && n.NoteDate <= endDate)
                    .Select(n => new
                    {
                        id = n.Id,
                        title = n.Title,
                        description = n.Description,
                        start = n.NoteDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        isCompleted = n.IsCompleted
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = notes });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Notlar alınırken bir hata oluştu", details = ex.Message });
            }
        }

        [HttpPost("notes")]
        public async Task<IActionResult> AddNote([FromBody] CalendarNoteRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Not verisi boş olamaz" });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var note = new CalendarNote
                {
                    Title = request.Title,
                    Description = request.Description,
                    NoteDate = request.NoteDate,
                    IsCompleted = request.IsCompleted,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    NotificationSent = false
                };

                var validationErrors = ValidateNote(note);
                if (validationErrors.Any())
                {
                    return BadRequest(new { success = false, error = "Doğrulama hataları", details = validationErrors });
                }

                _context.CalendarNotes.Add(note);
                await _context.SaveChangesAsync();

                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId,
                    ActivityType.CalendarNoteCreated,
                    "Takvim notu oluşturuldu",
                    note.Title,
                    note.Id,
                    "CalendarNote"
                );

                var response = new
                {
                    success = true,
                    data = new
                    {
                        id = note.Id,
                        title = note.Title,
                        description = note.Description,
                        start = note.NoteDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        isCompleted = note.IsCompleted
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Not eklenirken bir hata oluştu", details = ex.Message });
            }
        }

        [HttpPut("notes/{id}")]
        public async Task<IActionResult> UpdateNote(int id, [FromBody] CalendarNoteRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { success = false, error = "Not verisi boş olamaz" });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var note = await _context.CalendarNotes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (note == null)
                {
                    return NotFound(new { success = false, error = "Not bulunamadı" });
                }

                note.Title = request.Title;
                note.Description = request.Description;
                note.NoteDate = request.NoteDate;
                note.IsCompleted = request.IsCompleted;
                note.UpdatedAt = DateTime.UtcNow;

                var validationErrors = ValidateNote(note);
                if (validationErrors.Any())
                {
                    return BadRequest(new { success = false, error = "Doğrulama hataları", details = validationErrors });
                }

                await _context.SaveChangesAsync();

                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId,
                    ActivityType.CalendarNoteUpdated,
                    "Takvim notu güncellendi",
                    note.Title,
                    note.Id,
                    "CalendarNote"
                );

                return Ok(new { 
                    success = true,
                    data = new {
                        id = note.Id,
                        title = note.Title,
                        description = note.Description,
                        start = note.NoteDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        isCompleted = note.IsCompleted
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Not güncellenirken bir hata oluştu", details = ex.Message });
            }
        }

        [HttpDelete("notes/{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var note = await _context.CalendarNotes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (note == null)
                {
                    return NotFound(new { success = false, error = "Not bulunamadı" });
                }

                string noteTitle = note.Title; // Etkinlik kaydı için başlığı sakla
                
                _context.CalendarNotes.Remove(note);
                await _context.SaveChangesAsync();

                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId,
                    ActivityType.CalendarNoteDeleted,
                    "Takvim notu silindi",
                    noteTitle,
                    null,
                    "CalendarNote"
                );

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Not silinirken bir hata oluştu", details = ex.Message });
            }
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> CheckNotifications()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var today = DateTime.UtcNow.Date;
                var notifications = await _context.CalendarNotes
                    .Where(n => n.UserId == userId && 
                               n.NoteDate.Date == today && 
                               !n.NotificationSent &&
                               !n.IsCompleted)
                    .ToListAsync();

                foreach (var note in notifications)
                {
                    note.NotificationSent = true;
                }
                
                await _context.SaveChangesAsync();

                return Ok(new {
                    success = true,
                    data = notifications.Select(n => new
                    {
                        id = n.Id,
                        title = n.Title,
                        description = n.Description
                    })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Bildirimler kontrol edilirken bir hata oluştu", details = ex.Message });
            }
        }

        private List<string> ValidateNote(CalendarNote note)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(note.Title))
            {
                errors.Add("Başlık boş olamaz");
            }

            if (note.NoteDate == default)
            {
                errors.Add("Geçerli bir tarih seçmelisiniz");
            }

            return errors;
        }
    }

    public class CalendarNoteRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime NoteDate { get; set; }
        public bool IsCompleted { get; set; }
    }

    [Authorize]
    public class CalendarController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CalendarController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            
            if (user != null)
            {
                ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                ViewData["UserEmail"] = user.Email;
                ViewData["UserProfileImage"] = user.ProfileImageUrl;
                ViewData["CurrentUserId"] = user.Id;
            }

            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bilgileri bulunamadı.";
                return RedirectToAction("Index", "Dashboard");
            }

            // Takvim notunu veritabanından getir
            var note = await _context.CalendarNotes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

            if (note == null)
            {
                TempData["ErrorMessage"] = "Takvim notu bulunamadı veya not sizin değil.";
                return RedirectToAction("Index");
            }

            ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
            ViewData["UserEmail"] = user.Email;
            ViewData["UserProfileImage"] = user.ProfileImageUrl;
            ViewData["CurrentUserId"] = user.Id;

            // Notu görüntülemek için view'e gönder
            return RedirectToAction("Index", new { noteId = id });
        }
    }
} 