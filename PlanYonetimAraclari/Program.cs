using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Data;
using PlanYonetimAraclari.Models;
using PlanYonetimAraclari.Services;
using PlanYonetimAraclari.Extensions;
using PlanYonetimAraclari.Services.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Veritabanı bağlantısı
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// E-posta ayarlarını yapılandır
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();

// Proje servisi ekle
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<IPlannerService, PlannerService>();

// Görev ve takvim servisleri ekle
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();

// Profil resmi kontrol servisi ekle
builder.Services.AddScoped<ProfileImageHelper>();

// Arka plan servisleri
builder.Services.AddHostedService<ActivityCleanupService>();

// Identity yapılandırması
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => 
{
    // Şifre gereksinimleri
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
    // Oturum süresi ayarları - 5 saat
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromHours(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // Kullanıcı ayarları
    options.User.RequireUniqueEmail = true;
    
    // E-posta doğrulaması için ayarlar (Şifre sıfırlama için)
    options.SignIn.RequireConfirmedEmail = false;  // Geliştirme aşamasında false
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders(); // Token sağlayıcılar (şifre sıfırlama için)

// Oturum ve çerez ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(5);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.Cookie.Name = "Plan345Auth";
    options.Cookie.SameSite = SameSiteMode.Lax;
    
    // Yönlendirme döngüsünü önlemek için
    options.ReturnUrlParameter = "returnUrl";
    options.Events.OnRedirectToLogin = context =>
    {
        // API istekleri için 401 döndür, normal sayfalar için yönlendirme yap
        if (context.Request.Path.StartsWithSegments("/api") || 
            context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Add services to the container.
builder.Services.AddControllersWithViews(options => 
{
    // Global bildirim filtresini ekle
    options.Filters.Add<NotificationActionFilter>();
});

// SignalR servisini ekle
builder.Services.AddSignalR();

// Session servisi ekle
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(5); // Session süresi
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Veritabanını ve rolleri başlangıçta oluştur
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        
        // Veritabanını oluştur
        dbContext.Database.Migrate();
        
        // Rolleri ve admin kullanıcısını oluştur
        RoleInitializer.InitializeAsync(roleManager, userManager).Wait();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Rolleri oluştururken bir hata oluştu.");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session middleware'ini kullan - authentication'dan önce olmalı
app.UseSession();

// Kimlik doğrulama ve yetkilendirme
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Profil sayfası için route kontrolü
app.MapControllerRoute(
    name: "profile",
    pattern: "Profile/{action=Index}/{id?}",
    defaults: new { controller = "Profile" });

// Dashboard sayfası için route kontrolü
app.MapControllerRoute(
    name: "dashboard",
    pattern: "Dashboard/{action=Index}/{id?}",
    defaults: new { controller = "Dashboard" });

// SignalR Hub endpoint'i
app.MapHub<PlanYonetimAraclari.Hubs.TaskHub>("/taskHub");

app.Run();
