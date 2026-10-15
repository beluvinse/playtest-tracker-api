using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Options;

namespace PlaytestTracker.Api.Services
{
    // What the demo needs from time to time so it stays a good place to look around:
    //  1. Its projects and bugs go back to how they were first made (visitors can edit and delete
    //     them, so after a while there may be nothing left to see).
    //  2. The visitors who signed up long ago are deleted, with the organizations they own. The
    //     database is free and small, and every sign-up would otherwise stay in it forever.
    // The public demo accounts (luna, theo...) are never deleted.
    public class DemoCleaner
    {
        private readonly AppDbContext _context;
        private readonly DemoSeeder _seeder;
        private readonly IOptions<DemoOptions> _demo;
        private readonly IOptions<MaintenanceOptions> _maintenance;
        private readonly TimeProvider _clock;
        private readonly ILogger<DemoCleaner> _logger;

        public DemoCleaner(
            AppDbContext context,
            DemoSeeder seeder,
            IOptions<DemoOptions> demo,
            IOptions<MaintenanceOptions> maintenance,
            TimeProvider clock,
            ILogger<DemoCleaner> logger)
        {
            _context = context;
            _seeder = seeder;
            _demo = demo;
            _maintenance = maintenance;
            _clock = clock;
            _logger = logger;
        }

        public async Task<CleanupResult> CleanAsync()
        {
            if (!_demo.Value.Enabled)
                return new CleanupResult();

            var demo = await _context.Organizations.FirstOrDefaultAsync(organization => organization.IsDemo);
            if (demo == null)
                return new CleanupResult();

            // Both chores or none: a failure halfway must not leave the demo half done
            await using var transaction = await _context.Database.BeginTransactionAsync();

            await _seeder.RestoreContentAsync();
            var (deleted, kept) = await DeleteOldVisitorsAsync(demo.Id);

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Demo cleanup: content restored, {Deleted} visitors deleted, {Kept} kept for now.", deleted, kept);

            return new CleanupResult { DemoRestored = true, VisitorsDeleted = deleted, VisitorsKept = kept };
        }

        private async Task<(int Deleted, int Kept)> DeleteOldVisitorsAsync(int demoId)
        {
            // When somebody signs up they join the demo, so the date they joined it is the date
            // they signed up
            var cutoff = _clock.GetUtcNow().AddDays(-_maintenance.Value.VisitorMaxAgeDays);

            // Identity keeps emails in uppercase for lookups
            var demoEmails = DemoSeeder.People.Select(person => person.Email.ToUpperInvariant()).ToList();

            var demoPeople = await _context.Users
                .Where(user => demoEmails.Contains(user.NormalizedEmail!))
                .Select(user => user.Id)
                .ToListAsync();

            var expired = await _context.Memberships
                .Where(membership => membership.OrganizationId == demoId
                    && membership.JoinedAt < cutoff
                    && !demoPeople.Contains(membership.UserId))
                .Select(membership => membership.UserId)
                .ToListAsync();

            if (expired.Count == 0)
                return (0, 0);

            // The organizations they own (each one has exactly one owner)
            var owned = await _context.Memberships
                .Where(membership => expired.Contains(membership.UserId)
                    && membership.Role == OrganizationRole.Owner
                    && !membership.Organization.IsDemo)
                .Select(membership => new { membership.OrganizationId, membership.UserId })
                .ToListAsync();
            var ownedIds = owned.Select(organization => organization.OrganizationId).Distinct().ToList();

            // An organization is only deleted if everybody in it is going away too, or is one of the
            // public demo accounts. If somebody else (a person who signed up recently, say) was added to
            // it, deleting it would take their work away: its owner stays until the next time.
            var shared = await _context.Memberships
                .Where(membership => ownedIds.Contains(membership.OrganizationId)
                    && !expired.Contains(membership.UserId)
                    && !demoPeople.Contains(membership.UserId))
                .Select(membership => membership.OrganizationId)
                .Distinct()
                .ToListAsync();

            var keptOwners = owned
                .Where(organization => shared.Contains(organization.OrganizationId))
                .Select(organization => organization.UserId)
                .ToHashSet();

            var toDelete = expired.Where(id => !keptOwners.Contains(id)).ToList();
            var organizationsToDelete = owned
                .Where(organization => !keptOwners.Contains(organization.UserId))
                .Select(organization => organization.OrganizationId)
                .ToList();

            // From the inside out: the database refuses to delete a project that still has bugs,
            // or an organization that still has projects
            await _context.Bugs
                .Where(bug => organizationsToDelete.Contains(bug.Project.OrganizationId))
                .ExecuteDeleteAsync();
            await _context.Projects
                .Where(project => organizationsToDelete.Contains(project.OrganizationId))
                .ExecuteDeleteAsync();

            // The memberships of a deleted organization or user go with them (cascade)
            await _context.Organizations
                .Where(organization => organizationsToDelete.Contains(organization.Id))
                .ExecuteDeleteAsync();
            await _context.Users
                .Where(user => toDelete.Contains(user.Id))
                .ExecuteDeleteAsync();

            return (toDelete.Count, keptOwners.Count);
        }
    }

    public class CleanupResult
    {
        public bool DemoRestored { get; set; }
        public int VisitorsDeleted { get; set; }

        // Visitors that were old enough but stay for now (they own an organization that other people use)
        public int VisitorsKept { get; set; }
    }
}
