using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        public DbSet<BugReport> Bugs { get; set; }
        public DbSet<Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BugReport>()
                .HasOne(bug => bug.Project)
                .WithMany(project => project.Bugs)
                .HasForeignKey(bug => bug.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
