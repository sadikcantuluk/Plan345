using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Services.BackgroundServices
{
    public class ActivityCleanupService : BackgroundService
    {
        private readonly ILogger<ActivityCleanupService> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        // 24 saatte bir çalıştır
        private readonly TimeSpan _period = TimeSpan.FromHours(24);

        public ActivityCleanupService(
            ILogger<ActivityCleanupService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(_period);
            
            // Uygulama başladığında ilk temizliği yap
            await CleanupActivitiesAsync();

            // Periyodik olarak devam et
            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                await CleanupActivitiesAsync();
            }
        }

        private async Task CleanupActivitiesAsync()
        {
            try
            {
                _logger.LogInformation("Eski etkinlik kayıtları temizleniyor...");
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var activityService = scope.ServiceProvider.GetRequiredService<ActivityService>();
                    var deletedCount = await activityService.CleanupOldActivitiesAsync();
                    
                    _logger.LogInformation("{Count} adet eski etkinlik kaydı temizlendi", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Etkinlik temizleme işlemi sırasında hata oluştu");
            }
        }
    }
} 