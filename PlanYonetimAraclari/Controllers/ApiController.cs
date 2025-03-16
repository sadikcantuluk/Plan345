using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ApiController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ILogger<ApiController> _logger;

        public ApiController(UserManager<ApplicationUser> userManager, IProjectService projectService, ILogger<ApiController> logger)
        {
            _userManager = userManager;
            _projectService = projectService;
            _logger = logger;
        }

        [HttpGet]
        [Route("GetUserProjects")]
        public async Task<IActionResult> GetUserProjects()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt to GetUserProjects");
                    return Unauthorized();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found for authenticated request");
                    return Unauthorized();
                }

                var projects = await _projectService.GetUserProjectsAsync(user.Id);
                var projectData = projects.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    status = (int)p.Status,
                    userId = p.UserId,
                    description = p.Description,
                    dueDate = p.DueDate,
                    createdDate = p.CreatedDate,
                    lastUpdatedDate = p.LastUpdatedDate,
                    teamMembers = p.TeamMembers?.Count ?? 0,
                    isOwner = p.UserId == user.Id
                });

                return Ok(projectData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Projeler alınırken hata oluştu: {ex.Message}");
                return StatusCode(500, "Projeler alınırken bir hata oluştu.");
            }
        }
    }
} 