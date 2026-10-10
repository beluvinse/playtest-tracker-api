using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Data
{
    // IdentityDbContext is a DbContext that also knows the user tables (AspNetUsers, AspNetRoles…),
    // so users live in the same database and the same migrations as the rest of the data
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        public DbSet<BugReport> Bugs { get; set; }
        public DbSet<Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity configures its own tables here, so this call must stay first
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BugReport>()
                .HasOne(bug => bug.Project)
                .WithMany(project => project.Bugs)
                .HasForeignKey(bug => bug.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // A unique index: the database itself refuses two projects with the same code,
            // even if two requests arrive at the same time
            modelBuilder.Entity<Project>()
                .HasIndex(project => project.Code)
                .IsUnique();
        }
    }
}
