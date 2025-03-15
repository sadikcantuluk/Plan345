using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlanYonetimAraclari.Models;

namespace PlanYonetimAraclari.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        public DbSet<ProjectModel> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // İsteğe bağlı yapılandırmalar burada yapılabilir
            // Örneğin, veritabanı tablolarının adlarını özelleştirebilirsiniz:
            // builder.Entity<ApplicationUser>().ToTable("Users");
        }
    }
} 