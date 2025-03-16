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
        public DbSet<TaskModel> Tasks { get; set; }
        public DbSet<TwoFactorCode> TwoFactorCodes { get; set; }
        public DbSet<ProjectTeamMember> ProjectTeamMembers { get; set; }
        public DbSet<ProjectInvitation> ProjectInvitations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // İsteğe bağlı yapılandırmalar burada yapılabilir
            // Örneğin, veritabanı tablolarının adlarını özelleştirebilirsiniz:
            // builder.Entity<ApplicationUser>().ToTable("Users");

            // Configure relationships and constraints
            builder.Entity<ProjectTeamMember>()
                .HasOne(ptm => ptm.Project)
                .WithMany(p => p.TeamMembers)
                .HasForeignKey(ptm => ptm.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ProjectTeamMember>()
                .HasOne(ptm => ptm.User)
                .WithMany()
                .HasForeignKey(ptm => ptm.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ProjectInvitation>()
                .HasOne(pi => pi.Project)
                .WithMany(p => p.Invitations)
                .HasForeignKey(pi => pi.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ProjectInvitation>()
                .HasOne(pi => pi.InvitedByUser)
                .WithMany()
                .HasForeignKey(pi => pi.InvitedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
} 