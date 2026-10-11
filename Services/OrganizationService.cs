using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    // Organizations as one person sees them. Like the rest, every method takes the id of the
    // person asking: an organization they aren't a member of is answered as "not found".
    public class OrganizationService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public OrganizationService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        // The organizations the person belongs to: the demo always last, the rest oldest first
        // (so their personal one comes first, even though the demo is older than their account)
        public async Task<List<OrganizationDto>> GetMineAsync(string userId)
        {
            return await Mine(userId)
                .OrderBy(organization => organization.IsDemo)
                .ThenBy(organization => organization.CreatedAt)
                .ThenBy(organization => organization.Id)
                .ToListAsync();
        }

        public async Task<OrganizationDto?> GetByIdAsync(string userId, int id)
        {
            return await Mine(userId).FirstOrDefaultAsync(organization => organization.Id == id);
        }

        // Whoever creates an organization is its Owner. The organization and its owner are saved in
        // one SaveChanges, which is one transaction: there is never an organization without owner.
        public async Task<OrganizationDto> CreateAsync(string userId, string name)
        {
            var now = _clock.GetUtcNow();

            var organization = new Organization
            {
                Name = name,
                CreatedAt = now,
                Members =
                {
                    new Membership { UserId = userId, Role = OrganizationRole.Owner, JoinedAt = now }
                }
            };

            _context.Organizations.Add(organization);
            await _context.SaveChangesAsync();

            return (await GetByIdAsync(userId, organization.Id))!;
        }

        // Only the Owner can rename it, and the demo organization can't be renamed by anybody
        public async Task<OrganizationOperationResult> RenameAsync(string userId, int id, string name)
        {
            var membership = await _context.Memberships
                .Include(m => m.Organization)
                .FirstOrDefaultAsync(m => m.OrganizationId == id && m.UserId == userId);

            if (membership == null)
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.NotFound };

            if (membership.Organization.IsDemo)
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.DemoLocked };

            if (membership.Role != OrganizationRole.Owner)
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.Forbidden };

            membership.Organization.Name = name;
            await _context.SaveChangesAsync();

            return new OrganizationOperationResult
            {
                Status = OrganizationOperationStatus.Success,
                Organization = await GetByIdAsync(userId, id)
            };
        }

        // Only the Owner can delete it, never the demo one, and never while it still has projects:
        // deleting them along with it would be too easy to do by accident (same rule as deleting
        // a project that has bugs). Its memberships go with it; the people don't.
        public async Task<OrganizationOperationResult> DeleteAsync(string userId, int id)
        {
            var membership = await _context.Memberships
                .Include(m => m.Organization)
                .FirstOrDefaultAsync(m => m.OrganizationId == id && m.UserId == userId);

            if (membership == null)
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.NotFound };

            if (membership.Organization.IsDemo)
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.DemoLocked };

            if (membership.Role != OrganizationRole.Owner)
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.Forbidden };

            if (await _context.Projects.AnyAsync(project => project.OrganizationId == id))
                return new OrganizationOperationResult { Status = OrganizationOperationStatus.HasProjects };

            _context.Organizations.Remove(membership.Organization);
            await _context.SaveChangesAsync();

            return new OrganizationOperationResult { Status = OrganizationOperationStatus.Success };
        }

        // Starts from the person's memberships, so only their organizations can come out of it
        private IQueryable<OrganizationDto> Mine(string userId)
        {
            return _context.Memberships
                .Where(membership => membership.UserId == userId)
                .Select(membership => new OrganizationDto
                {
                    Id = membership.OrganizationId,
                    Name = membership.Organization.Name,
                    IsDemo = membership.Organization.IsDemo,
                    CreatedAt = membership.Organization.CreatedAt,
                    MyRole = membership.Role,
                    MemberCount = membership.Organization.Members.Count(),
                    ProjectCount = _context.Projects.Count(project =>
                        project.OrganizationId == membership.OrganizationId)
                });
        }
    }
}
