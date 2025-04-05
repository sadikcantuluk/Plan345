using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class TaskPlannerController : Controller
    {
        private readonly IPlannerService _plannerService;
        private readonly IProjectService _projectService;
        private readonly ILogger<TaskPlannerController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskPlannerController(
            IPlannerService plannerService,
            IProjectService projectService,
            ILogger<TaskPlannerController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _plannerService = plannerService;
            _projectService = projectService;
            _logger = logger;
            _userManager = userManager;
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var userProjects = await _projectService.GetUserProjectsAsync(userId);
                ViewData["UserProjects"] = userProjects;
                ViewData["CurrentUserId"] = userId;

                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                    ViewData["UserEmail"] = user.Email;
                    ViewData["UserProfileImage"] = user.ProfileImageUrl;
                    
                    // Bildirim kontrolü eklenmeli 
                    ViewData["HasAnyNotifications"] = false; // Şimdilik false, gerçek entegrasyonda bildirim servisi kullanılmalı
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Task Planner Index");
                return View("Error");
            }
        }

        [HttpPost]
        [Route("SaveTask")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTask([FromBody] PlannerTask plannerTask)
        {
            _logger.LogInformation("SaveTask çağrıldı: {@PlannerTask}", new { 
                plannerTask?.Name, 
                plannerTask?.Description, 
                plannerTask?.StartDate, 
                plannerTask?.EndDate,
                plannerTask?.UserId,
                plannerTask?.ParentTaskId
            });
            
            // ModelState hatalarını detaylı loglayalım
            if (!ModelState.IsValid)
            {
                var modelStateErrors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new { 
                        Key = x.Key, 
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage).ToList() 
                    })
                    .ToList();
                
                _logger.LogWarning("ModelState hataları: {@ModelStateErrors}", modelStateErrors);
                return Json(new { success = false, error = $"Geçersiz form verisi: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}" });
            }
            
            try
            {
                if (plannerTask == null)
                {
                    _logger.LogWarning("SaveTask null plannerTask");
                    return Json(new { success = false, error = "Geçersiz veri: Görev bilgileri boş." });
                }
                
                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning("SaveTask model geçersiz: {Errors}", errors);
                    return Json(new { success = false, error = $"Geçersiz form verisi: {errors}" });
                }
                
                // Ek doğrulamalar
                if (string.IsNullOrWhiteSpace(plannerTask.Name))
                {
                    return Json(new { success = false, error = "Proje adı zorunludur." });
                }
                
                if (plannerTask.Name.Length > 80)
                {
                    return Json(new { success = false, error = "Proje adı 80 karakterden uzun olamaz." });
                }
                
                if (!string.IsNullOrEmpty(plannerTask.Description) && plannerTask.Description.Length > 500)
                {
                    return Json(new { success = false, error = "Açıklama 500 karakterden uzun olamaz." });
                }
                
                if (plannerTask.StartDate > plannerTask.EndDate)
                {
                    return Json(new { success = false, error = "Başlangıç tarihi bitiş tarihinden sonra olamaz." });
                }

                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, error = "Kullanıcı kimliği bulunamadı." });
                }
                
                plannerTask.UserId = userId;
                
                // Eksik alanları doldur
                if (plannerTask.CreatedAt == default)
                {
                    plannerTask.CreatedAt = DateTime.Now;
                }
                
                if (plannerTask.Duration == 0)
                {
                    // Gün bazında süreyi hesapla
                    plannerTask.Duration = (int)Math.Ceiling((plannerTask.EndDate - plannerTask.StartDate).TotalDays);
                }
                
                // User nesnesini null olarak ayarla, EF Core kendisi ekleyecek
                plannerTask.User = null;

                // Giriş verilerini temizle
                plannerTask.Name = plannerTask.Name.Trim();
                plannerTask.Description = plannerTask.Description?.Trim();

                // Hiyerarşik yapı için sıra ayarlama
                if (plannerTask.ParentTaskId.HasValue)
                {
                    var parentTask = await _plannerService.GetTaskByIdAsync(plannerTask.ParentTaskId.Value);
                    if (parentTask == null)
                    {
                        return Json(new { success = false, error = "Üst görev bulunamadı." });
                    }

                    // Alt görevlerin sayısını getir ve yeni görevi sona ekle
                    var siblingTasks = await _plannerService.GetChildTasksAsync(plannerTask.ParentTaskId.Value);
                    plannerTask.OrderIndex = siblingTasks.Count();
                }
                else
                {
                    // Ana görev ise diğer ana görevlerin sonuna ekle
                    var rootTasks = await _plannerService.GetRootTasksAsync(userId);
                    plannerTask.OrderIndex = rootTasks.Count();
                }

                // Id > 0 ise güncelleme, değilse yeni kayıt
                if (plannerTask.Id > 0)
                {
                    // Güncelleme işlemi
                    await _plannerService.UpdateTaskAsync(plannerTask);
                }
                else
                {
                    // Yeni kayıt ekleme - Id alanını sıfırla
                    plannerTask.Id = 0;
                    await _plannerService.SaveTaskAsync(plannerTask);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving task");
                return Json(new { success = false, error = "Proje eklenirken bir hata oluştu: " + ex.Message });
            }
        }

        [HttpGet]
        [Route("GetTasks")]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetTasks: Kullanıcı kimliği alınamadı");
                    return StatusCode(500, new { error = "Kullanıcı kimliği alınamadı." });
                }
                
                _logger.LogInformation("GetTasks: Kullanıcı görevleri alınıyor. UserId: {UserId}", userId);
                
                try 
                {
                    var plannerTasks = await _plannerService.GetTasksAsync(userId);
                    
                    // Task bilgilerini serialize etmeden önce kontrol et
                    var taskList = plannerTasks.Select(t => new {
                        id = t.Id,
                        name = t.Name,
                        description = t.Description,
                        projectId = t.ProjectId,
                        parentTaskId = t.ParentTaskId,
                        startDate = t.StartDate,
                        endDate = t.EndDate,
                        taskState = t.TaskState,
                        completionPercentage = t.TaskState == 2 ? 100 : 0 // Tamamlandı durumdaysa %100
                    }).ToList();
                    
                    _logger.LogInformation("GetTasks: {Count} adet görev bulundu", taskList.Count);
                    return Json(taskList);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "GetTasks: PlannerService.GetTasksAsync çağrısında özel hata: {ErrorMessage}", innerEx.Message);
                    if (innerEx.InnerException != null)
                    {
                        _logger.LogError("GetTasks: İç hata: {InnerErrorMessage}", innerEx.InnerException.Message);
                    }
                    throw; // Dış catch bloğuna iletim
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks: {ErrorMessage}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerErrorMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, new { error = "Görevler alınırken bir hata oluştu: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("UpdateTask")]
        public async Task<IActionResult> UpdateTask([FromBody] ProjectViewModel taskViewModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                
                if (!taskViewModel.Id.HasValue)
                {
                    return BadRequest(new { success = false, error = "Görev ID'si belirtilmedi" });
                }
                
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                // Önce mevcut görevi al
                var existingTask = await _plannerService.GetTaskByIdAsync(taskViewModel.Id.Value);
                if (existingTask == null)
                {
                    return NotFound(new { success = false, error = "Görev bulunamadı" });
                }
                
                // Erişim kontrolü
                if (existingTask.UserId != userId)
                {
                    return Forbid();
                }
                
                // Görev bilgilerini güncelle
                existingTask.Name = taskViewModel.Name;
                existingTask.Description = taskViewModel.Description;
                existingTask.StartDate = DateTime.Parse(taskViewModel.StartDate);
                existingTask.EndDate = DateTime.Parse(taskViewModel.EndDate);
                
                await _plannerService.UpdateTaskAsync(existingTask);
                return Json(new { success = true, taskId = existingTask.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task");
                return Json(new { success = false, error = "Görev güncellenirken bir hata oluştu: " + ex.Message });
            }
        }

        [HttpDelete]
        [Route("DeleteTask")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                // Görevin kullanıcıya ait olup olmadığını doğrula
                var plannerTask = await _plannerService.GetTaskByIdAsync(id);
                if (plannerTask == null || plannerTask.UserId != userId)
                {
                    return Json(new { success = false, error = "Bu projeyi silme yetkiniz yok." });
                }

                // Recursive olarak alt görevlerle birlikte sil
                await _plannerService.DeleteTaskWithChildrenAsync(id);
                
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task");
                return Json(new { success = false, error = "Proje silinirken bir hata oluştu: " + ex.Message });
            }
        }

        [HttpGet]
        [Route("GetChildTasks")]
        public async Task<IActionResult> GetChildTasks(int parentId)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var childTasks = await _plannerService.GetChildTasksAsync(parentId);
                
                // Görevlerin kullanıcıya ait olup olmadığını doğrula
                var userTasks = childTasks.Where(t => t.UserId == userId).ToList();
                
                return Json(userTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving child tasks");
                return StatusCode(500, new { error = "Alt görevler alınırken bir hata oluştu." });
            }
        }

        [HttpGet]
        [Route("GetRootTasks")]
        public async Task<IActionResult> GetRootTasks()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var rootTasks = await _plannerService.GetRootTasksAsync(userId);
                return Json(rootTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving root tasks");
                return StatusCode(500, new { error = "Ana görevler alınırken bir hata oluştu." });
            }
        }
        
        [HttpGet]
        [Route("ProjectDetail/{id}")]
        public async Task<IActionResult> ProjectDetail(int id)
        {
            try
            {
                _logger.LogInformation("ProjectDetail çağrıldı. ProjectId: {ProjectId}", id);
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                // Kullanıcı bilgilerini ViewData'ya ekle
                ViewData["CurrentUserId"] = userId;
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserFullName"] = $"{user.FirstName} {user.LastName}";
                    ViewData["UserEmail"] = user.Email;
                    ViewData["UserProfileImage"] = user.ProfileImageUrl;
                    ViewData["HasAnyNotifications"] = false; // Şimdilik false
                }
                
                // Bu ID numarası ile bir planner task var mı kontrol et
                var plannerTask = await _plannerService.GetTaskByIdAsync(id);
                if (plannerTask == null)
                {
                    _logger.LogWarning("ProjectDetail: Task bulunamadı. Id: {ProjectId}", id);
                    return NotFound();
                }
                
                // Task kullanıcıya mı ait
                if (plannerTask.UserId != userId)
                {
                    _logger.LogWarning("ProjectDetail: Yetkisiz erişim. TaskId: {TaskId}, UserId: {UserId}", id, userId);
                    return Forbid();
                }
                
                // Tüm görevleri getir - bu şekilde tüm alt seviye görevleri doğru şekilde hiyerarşik gösterebiliriz
                var allUserTasks = await _plannerService.GetTasksAsync(userId);
                
                // Ana görev ve onun tüm alt görevlerini filtrele
                var projectTasks = allUserTasks
                    .Where(t => t.Id == id || IsDescendantOf(t, id, allUserTasks))
                    .ToList();
                
                ViewData["Task"] = plannerTask;
                ViewData["ChildTasks"] = projectTasks;
                
                _logger.LogInformation("ProjectDetail: Task detayları başarıyla getirildi. TaskId: {TaskId}, ChildTaskCount: {ChildCount}", 
                    id, projectTasks.Count);
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Project Detail for ID {ProjectId}: {ErrorMessage}", id, ex.Message);
                return View("Error");
            }
        }

        // Bir görevin belirli bir ID'nin alt görevi olup olmadığını kontrol eder
        private bool IsDescendantOf(PlannerTask task, int ancestorId, List<PlannerTask> allTasks)
        {
            if (task.ParentTaskId == null)
                return false;
                
            if (task.ParentTaskId == ancestorId)
                return true;
                
            // Ebeveyn görevi bul ve bu işlemi recursive olarak yürüt
            var parentTask = allTasks.FirstOrDefault(t => t.Id == task.ParentTaskId);
            if (parentTask == null)
                return false;
                
            return IsDescendantOf(parentTask, ancestorId, allTasks);
        }

        [HttpGet]
        [Route("GetProjects")]
        public async Task<IActionResult> GetProjects()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var projects = await _projectService.GetUserProjectsAsync(userId);
                
                var projectData = projects.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    status = (int)p.Status,
                    userId = p.UserId,
                    description = p.Description,
                    dueDate = p.DueDate,
                    createdDate = p.CreatedDate,
                    lastUpdatedDate = p.LastUpdatedDate
                });
                
                return Json(projectData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                return StatusCode(500, new { error = "Projeler alınırken bir hata oluştu." });
            }
        }

        [HttpPost]
        [Route("CreateProject")]
        public async Task<IActionResult> CreateProject([FromBody] ProjectViewModel projectViewModel)
        {
            try
            {
                _logger.LogInformation("CreateProject çağrıldı: {@ProjectViewModel}", new {
                    projectViewModel?.Name,
                    projectViewModel?.Description,
                    projectViewModel?.StartDate,
                    projectViewModel?.EndDate
                });
                
                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning("CreateProject model geçersiz: {Errors}", errors);
                    return BadRequest(new { success = false, error = $"Geçersiz form verisi: {errors}" });
                }
                
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("CreateProject: Kullanıcı kimliği alınamadı");
                    return BadRequest(new { success = false, error = "Kullanıcı kimliği alınamadı." });
                }
                
                // PlannerTask olarak yeni bir ana proje görevi oluştur
                var plannerTask = new PlannerTask
                {
                    Name = projectViewModel.Name.Trim(),
                    Description = projectViewModel.Description?.Trim(),
                    StartDate = DateTime.Parse(projectViewModel.StartDate),
                    EndDate = DateTime.Parse(projectViewModel.EndDate),
                    UserId = userId,
                    TaskState = 0, // Bekliyor durumu
                    CreatedAt = DateTime.Now,
                    ParentTaskId = null, // Ana görev (proje)
                    OrderIndex = 0
                };
                
                // Gerekli doğrulamaları yap
                if (string.IsNullOrWhiteSpace(plannerTask.Name))
                {
                    return BadRequest(new { success = false, error = "Proje adı boş olamaz." });
                }
                
                if (plannerTask.StartDate > plannerTask.EndDate)
                {
                    return BadRequest(new { success = false, error = "Başlangıç tarihi bitiş tarihinden sonra olamaz." });
                }
                
                // Eğer ProjectTypeId belirtilmişse
                if (projectViewModel.ProjectTypeId.HasValue)
                {
                    plannerTask.ProjectId = projectViewModel.ProjectTypeId.Value;
                }
                
                _logger.LogInformation("Yeni TaskPlanner projesi oluşturuluyor: {ProjectName}", plannerTask.Name);
                
                // Ana proje görevini kaydet - otomatik alt görev oluşturma kaldırıldı
                await _plannerService.SaveTaskAsync(plannerTask);
                
                if (plannerTask.Id <= 0)
                {
                    _logger.LogWarning("Proje oluşturulamadı");
                    return Json(new { success = false, error = "Proje oluşturulamadı." });
                }
                
                _logger.LogInformation("Proje başarıyla oluşturuldu: {ProjectId}", plannerTask.Id);
                return Json(new { success = true, projectId = plannerTask.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                return Json(new { success = false, error = "Proje oluşturulurken bir hata oluştu: " + ex.Message });
            }
        }
        
        // ViewModel sınıfları
        public class ProjectViewModel
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public int? ProjectTypeId { get; set; }
        }

        [HttpGet]
        [Route("GetTask/{id}")]
        public async Task<IActionResult> GetTask(int id)
        {
            try
            {
                _logger.LogInformation("GetTask çağrıldı. TaskId: {TaskId}", id);
                
                if (id <= 0)
                {
                    _logger.LogWarning("GetTask: Geçersiz görev ID: {TaskId}", id);
                    return NotFound(new { error = "Geçersiz görev ID." });
                }
                
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetTask: Kullanıcı kimliği alınamadı");
                    return StatusCode(401, new { error = "Kullanıcı kimliği alınamadı. Lütfen tekrar giriş yapın." });
                }
                
                var task = await _plannerService.GetTaskByIdAsync(id);
                
                if (task == null)
                {
                    _logger.LogWarning("GetTask: Görev bulunamadı. TaskId: {TaskId}", id);
                    return NotFound(new { error = "Görev bulunamadı." });
                }
                
                if (task.UserId != userId)
                {
                    _logger.LogWarning("GetTask: Yetkisiz erişim. TaskId: {TaskId}, UserId: {UserId}, TaskUserId: {TaskUserId}", 
                        id, userId, task.UserId);
                    return StatusCode(403, new { error = "Bu göreve erişim yetkiniz yok." });
                }
                
                _logger.LogInformation("GetTask: Görev başarıyla alındı. TaskId: {TaskId}", id);
                
                var result = new { 
                    id = task.Id,
                    projectId = task.ProjectId,
                    name = task.Name,
                    description = task.Description,
                    startDate = task.StartDate,
                    endDate = task.EndDate,
                    parentTaskId = task.ParentTaskId,
                    taskState = task.TaskState
                };
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTask: Görev alınırken hata oluştu. TaskId: {TaskId}, Hata: {ErrorMessage}", id, ex.Message);
                return StatusCode(500, new { error = "Görev alınırken bir hata oluştu: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("UpdateTaskState")]
        public async Task<IActionResult> UpdateTaskState([FromBody] TaskStateViewModel model)
        {
            _logger.LogInformation("UpdateTaskState çağrıldı. TaskId: {TaskId}, State: {State}", model.TaskId, model.State);
            
            try
            {
                if (model == null)
                {
                    _logger.LogWarning("UpdateTaskState: model null");
                    return Json(new { success = false, error = "Geçersiz veri." });
                }

                // Kullanıcı kimliğini al
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("UpdateTaskState: Kullanıcı kimliği alınamadı");
                    return Json(new { success = false, error = "Kullanıcı kimliği alınamadı." });
                }

                // Görevi getir
                var task = await _plannerService.GetTaskByIdAsync(model.TaskId);
                if (task == null)
                {
                    _logger.LogWarning("UpdateTaskState: Görev bulunamadı. TaskId: {TaskId}", model.TaskId);
                    return Json(new { success = false, error = "Görev bulunamadı." });
                }

                // Yetki kontrolü - sadece kendi görevlerini güncelleyebilmeli
                if (task.UserId != userId)
                {
                    _logger.LogWarning("UpdateTaskState: Yetki hatası. TaskId: {TaskId}, TaskUserId: {TaskUserId}, RequestUserId: {RequestUserId}", 
                        model.TaskId, task.UserId, userId);
                    return Json(new { success = false, error = "Bu görevi güncelleme yetkiniz yok." });
                }

                // Durum geçerli mi kontrol et (0, 1, 2, 3 olabilir)
                if (model.State < 0 || model.State > 3)
                {
                    _logger.LogWarning("UpdateTaskState: Geçersiz durum. State: {State}", model.State);
                    return Json(new { success = false, error = "Geçersiz görev durumu." });
                }

                // Durumu güncelle
                task.TaskState = model.State;
                await _plannerService.UpdateTaskAsync(task);

                _logger.LogInformation("UpdateTaskState: Görev durumu başarıyla güncellendi. TaskId: {TaskId}, NewState: {State}", model.TaskId, model.State);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Görev durumu güncellenirken hata: {ErrorMessage}", ex.Message);
                return Json(new { success = false, error = "Görev durumu güncellenirken bir hata oluştu: " + ex.Message });
            }
        }
        
        public class TaskStateViewModel
        {
            public int TaskId { get; set; }
            public int State { get; set; }
        }

        public class TaskParentViewModel
        {
            public int TaskId { get; set; }
            public int ParentTaskId { get; set; }
        }

        [HttpPost]
        [Route("UpdateTaskParent")]
        public async Task<IActionResult> UpdateTaskParent([FromBody] TaskParentViewModel model)
        {
            _logger.LogInformation("UpdateTaskParent çağrıldı. TaskId: {TaskId}, ParentTaskId: {ParentTaskId}", 
                model.TaskId, model.ParentTaskId);
            
            try
            {
                if (model == null)
                {
                    _logger.LogWarning("UpdateTaskParent: model null");
                    return Json(new { success = false, error = "Geçersiz veri." });
                }

                // Kullanıcı kimliğini al
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("UpdateTaskParent: Kullanıcı kimliği alınamadı");
                    return Json(new { success = false, error = "Kullanıcı kimliği alınamadı." });
                }

                // Taşınacak görevi getir
                var task = await _plannerService.GetTaskByIdAsync(model.TaskId);
                if (task == null)
                {
                    _logger.LogWarning("UpdateTaskParent: Görev bulunamadı. TaskId: {TaskId}", model.TaskId);
                    return Json(new { success = false, error = "Görev bulunamadı." });
                }

                // Hedef ebeveyn görevi getir
                var parentTask = await _plannerService.GetTaskByIdAsync(model.ParentTaskId);
                if (parentTask == null)
                {
                    _logger.LogWarning("UpdateTaskParent: Ebeveyn görev bulunamadı. ParentTaskId: {ParentTaskId}", model.ParentTaskId);
                    return Json(new { success = false, error = "Ebeveyn görev bulunamadı." });
                }

                // Yetki kontrolü - sadece kendi görevlerini güncelleyebilmeli
                if (task.UserId != userId || parentTask.UserId != userId)
                {
                    _logger.LogWarning("UpdateTaskParent: Yetki hatası. TaskId: {TaskId}, ParentTaskId: {ParentTaskId}, RequestUserId: {RequestUserId}", 
                        model.TaskId, model.ParentTaskId, userId);
                    return Json(new { success = false, error = "Bu görevleri güncelleme yetkiniz yok." });
                }

                // Döngüsel bağımlılık kontrolü
                if (await IsCircularDependency(model.TaskId, model.ParentTaskId))
                {
                    _logger.LogWarning("UpdateTaskParent: Döngüsel bağımlılık tespit edildi. TaskId: {TaskId}, ParentTaskId: {ParentTaskId}", 
                        model.TaskId, model.ParentTaskId);
                    return Json(new { success = false, error = "Döngüsel görev bağımlılığı oluşturulamaz." });
                }

                // Ebeveyn görevini güncelle
                task.ParentTaskId = model.ParentTaskId;
                
                // Alt görevlerin sayısını getir ve yeni görevi sona ekle
                var siblingTasks = await _plannerService.GetChildTasksAsync(model.ParentTaskId);
                task.OrderIndex = siblingTasks.Count();
                
                await _plannerService.UpdateTaskAsync(task);

                _logger.LogInformation("UpdateTaskParent: Görev hiyerarşisi başarıyla güncellendi. TaskId: {TaskId}, NewParentId: {ParentTaskId}", 
                    model.TaskId, model.ParentTaskId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateTaskParent: Görev hiyerarşisi güncellenirken hata oluştu. TaskId: {TaskId}, Error: {ErrorMessage}", 
                    model.TaskId, ex.Message);
                return Json(new { success = false, error = "Görev hiyerarşisi güncellenirken bir hata oluştu: " + ex.Message });
            }
        }
        
        // Döngüsel bağımlılık kontrolü için yardımcı metod
        private async Task<bool> IsCircularDependency(int taskId, int newParentId)
        {
            // Görevin kendisine bağlanmasını engelle
            if (taskId == newParentId)
                return true;
                
            var currentParentId = newParentId;
            var visited = new HashSet<int>();
            
            // Ebeveyn zincirini takip et
            while (currentParentId != 0)
            {
                // Zaten ziyaret edilmiş bir görev varsa döngü var demektir
                if (visited.Contains(currentParentId))
                    return true;
                    
                visited.Add(currentParentId);
                
                // Eğer zincirde görevin kendisi varsa döngü var demektir
                if (currentParentId == taskId)
                    return true;
                    
                // Bir sonraki ebeveyni getir
                var parentTask = await _plannerService.GetTaskByIdAsync(currentParentId);
                if (parentTask == null || !parentTask.ParentTaskId.HasValue)
                    break;
                    
                currentParentId = parentTask.ParentTaskId.Value;
            }
            
            return false;
        }

        [HttpPost]
        [Route("GenerateMigration")]
        public IActionResult GenerateMigration()
        {
            return Content("This is a placeholder. Use 'dotnet ef migrations add' command in CLI for actual migration generation.");
        }
        
        [HttpGet]
        [Route("VerifyDatabase")]
        public async Task<IActionResult> VerifyDatabase()
        {
            try
            {
                _logger.LogInformation("TaskPlanner veritabanı doğrulama işlemi başlatılıyor...");
                
                // Veritabanı bağlantısını kontrol et
                var context = _plannerService.GetDbContext();
                var canConnect = await context.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    _logger.LogError("Veritabanına bağlantı kurulamadı");
                    return Json(new { 
                        success = false, 
                        message = "Veritabanına bağlantı kurulamıyor." 
                    });
                }
                
                // PlannerTasks tablosunun varlığını kontrol et
                bool tableExists = false;
                int taskCount = 0;
                
                try 
                {
                    // Basit bir sorgu ile kontrol ediyoruz
                    taskCount = await _plannerService.GetTaskCountAsync();
                    tableExists = true;
                    _logger.LogInformation("PlannerTasks tablosu bulundu, toplam görev sayısı: {TaskCount}", taskCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PlannerTasks tablosu kontrolü sırasında hata: {ErrorMessage}", ex.Message);
                }
                
                var result = new { 
                    success = true, 
                    databaseConnection = canConnect,
                    plannerTasksTableExists = tableExists,
                    taskCount = taskCount,
                    message = tableExists 
                        ? $"Veritabanı bağlantısı başarılı. PlannerTasks tablosu bulundu. Toplam {taskCount} görev mevcut." 
                        : "Veritabanı bağlantısı başarılı, ancak PlannerTasks tablosu bulunamadı. Migration gerekli olabilir."
                };
                
                _logger.LogInformation("Veritabanı doğrulama sonucu: {@Result}", new { 
                    result.success, 
                    result.databaseConnection, 
                    result.plannerTasksTableExists, 
                    result.taskCount 
                });
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Veritabanı doğrulama sırasında hata: {ErrorMessage}", ex.Message);
                return Json(new { 
                    success = false, 
                    message = $"Veritabanı doğrulama sırasında hata: {ex.Message}" 
                });
            }
        }
    }
} 