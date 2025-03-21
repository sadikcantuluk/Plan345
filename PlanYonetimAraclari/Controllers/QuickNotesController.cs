using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PlanYonetimAraclari.Services;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/quicknotes")]
    public class QuickNotesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ActivityService _activityService;

        public QuickNotesApiController(ApplicationDbContext context, ActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
        }

        // GET: api/quicknotes
        [HttpGet]
        public async Task<IActionResult> GetQuickNotes()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var notes = await _context.QuickNotes
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        id = n.Id,
                        content = n.Content,
                        createdAt = n.CreatedAt,
                        updatedAt = n.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = notes });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Notlar alınırken bir hata oluştu", details = ex.Message });
            }
        }

        // POST: api/quicknotes
        [HttpPost]
        public async Task<IActionResult> AddQuickNote([FromBody] QuickNoteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { success = false, error = "Not içeriği boş olamaz" });
            }
            
            if (request.Content.Length > 200)
            {
                return BadRequest(new { success = false, error = "Not içeriği maksimum 200 karakter olabilir" });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                var quickNote = new QuickNote
                {
                    Content = request.Content,
                    UserId = userId,
                    CreatedAt = DateTime.Now
                };

                _context.QuickNotes.Add(quickNote);
                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: userId,
                    type: ActivityType.NoteCreated,
                    title: "Yeni not eklendi",
                    description: quickNote.Content.Length > 50 ? quickNote.Content.Substring(0, 47) + "..." : quickNote.Content,
                    relatedEntityId: quickNote.Id,
                    relatedEntityType: "QuickNote"
                );

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = quickNote.Id,
                        content = quickNote.Content,
                        createdAt = quickNote.CreatedAt,
                        updatedAt = quickNote.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Not eklenirken bir hata oluştu", details = ex.Message });
            }
        }

        // PUT: api/quicknotes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuickNote(int id, [FromBody] QuickNoteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { success = false, error = "Not içeriği boş olamaz" });
            }
            
            if (request.Content.Length > 200)
            {
                return BadRequest(new { success = false, error = "Not içeriği maksimum 200 karakter olabilir" });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var quickNote = await _context.QuickNotes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (quickNote == null)
                {
                    return NotFound(new { success = false, error = "Not bulunamadı" });
                }

                quickNote.Content = request.Content;
                quickNote.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: userId,
                    type: ActivityType.NoteUpdated,
                    title: "Not güncellendi",
                    description: quickNote.Content.Length > 50 ? quickNote.Content.Substring(0, 47) + "..." : quickNote.Content,
                    relatedEntityId: quickNote.Id,
                    relatedEntityType: "QuickNote"
                );

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = quickNote.Id,
                        content = quickNote.Content,
                        createdAt = quickNote.CreatedAt,
                        updatedAt = quickNote.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Not güncellenirken bir hata oluştu", details = ex.Message });
            }
        }

        // DELETE: api/quicknotes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuickNote(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var quickNote = await _context.QuickNotes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (quickNote == null)
                {
                    return NotFound(new { success = false, error = "Not bulunamadı" });
                }
                
                // Not içeriğini saklayalım
                var noteContent = quickNote.Content.Length > 50 ? quickNote.Content.Substring(0, 47) + "..." : quickNote.Content;

                _context.QuickNotes.Remove(quickNote);
                await _context.SaveChangesAsync();
                
                // Etkinlik kaydı oluştur
                await _activityService.LogActivityAsync(
                    userId: userId,
                    type: ActivityType.NoteDeleted,
                    title: "Not silindi",
                    description: noteContent,
                    relatedEntityId: null,
                    relatedEntityType: "QuickNote"
                );

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Not silinirken bir hata oluştu", details = ex.Message });
            }
        }
    }

    public class QuickNoteRequest
    {
        public string Content { get; set; }
    }

    [Authorize]
    public class QuickNotesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public QuickNotesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // ViewData'ya kullanıcı bilgilerini ekle
            ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
            ViewData["UserEmail"] = user.Email;
            ViewData["UserProfileImage"] = user.ProfileImageUrl;
            ViewData["HasPendingInvitations"] = await _context.ProjectInvitations
                .AnyAsync(i => i.InvitedEmail == user.Email && i.Status == InvitationStatus.Pending);

            return View();
        }
    }
} 