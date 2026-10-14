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
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Membership> Memberships { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity configures its own tables here, so this call must stay first
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BugReport>()
                .HasOne(bug => bug.Project)
                .WithMany(project => project.Bugs)
                .HasForeignKey(bug => bug.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // A unique index: the database itself refuses two projects with the same code
            // inside one organization, even if two requests arrive at the same time.
            // Two different organizations can both have a "PIN".
            modelBuilder.Entity<Project>()
                .HasIndex(project => new { project.OrganizationId, project.Code })
                .IsUnique();

            // An organization with projects can't be deleted by accident: its projects wouldn't
            // silently disappear with it
            modelBuilder.Entity<Project>()
                .HasOne(project => project.Organization)
                .WithMany()
                .HasForeignKey(project => project.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // A person appears once per organization: the pair (organization, person) is the key
            modelBuilder.Entity<Membership>()
                .HasKey(membership => new { membership.OrganizationId, membership.UserId });

            // Deleting an organization deletes its memberships (they mean nothing without it)
            modelBuilder.Entity<Membership>()
                .HasOne(membership => membership.Organization)
                .WithMany(organization => organization.Members)
                .HasForeignKey(membership => membership.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Membership>()
                .HasOne(membership => membership.User)
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One owner per organization, enforced by the database itself. A "partial" unique
            // index only looks at the rows that match the filter: here, only the owners. So an
            // organization can have many members, but a second owner is refused, even if two
            // requests try to add one at the same moment.
            // (The filter is raw SQL, written for PostgreSQL: column names in double quotes.)
            modelBuilder.Entity<Membership>()
                .HasIndex(membership => membership.OrganizationId)
                .IsUnique()
                .HasFilter($"\"Role\" = {(int)OrganizationRole.Owner}")
                .HasDatabaseName("IX_Memberships_OneOwnerPerOrganization");
        }
    }
}
