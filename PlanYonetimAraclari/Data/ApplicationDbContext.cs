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
        public DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; }
        public DbSet<ProjectTeamMember> ProjectTeamMembers { get; set; }
        public DbSet<ProjectInvitation> ProjectInvitations { get; set; }
        public DbSet<CalendarNote> CalendarNotes { get; set; }
        public DbSet<QuickNote> QuickNotes { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<PlannerTask> PlannerTasks { get; set; }

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
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectTeamMember>()
                .HasOne(ptm => ptm.User)
                .WithMany()
                .HasForeignKey(ptm => ptm.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ProjectInvitation>()
                .HasOne(pi => pi.Project)
                .WithMany(p => p.Invitations)
                .HasForeignKey(pi => pi.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectInvitation>()
                .HasOne(pi => pi.InvitedByUser)
                .WithMany()
                .HasForeignKey(pi => pi.InvitedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure PlannerTask entity
            builder.Entity<PlannerTask>()
                .HasOne(pt => pt.Project)
                .WithMany()
                .HasForeignKey(pt => pt.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
} 