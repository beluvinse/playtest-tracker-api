using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    // Who is in an organization, and adding and removing people. As in the rest of the services,
    // every method takes the id of the person asking.
    //
    // Who may do what:
    //   - see the list of people: any member
    //   - add someone: the Owner
    //   - remove someone: the Owner, or the person themselves (leaving)
    //   - the Owner can't be removed, and the demo organization's people can't change
    public class MembershipService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public MembershipService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        // The people of an organization, the Owner first. Null when the person asking isn't a member.
        public async Task<List<MemberDto>?> GetMembersAsync(string userId, int organizationId)
        {
            if (!await IsMemberAsync(userId, organizationId))
                return null;

            return await _context.Memberships
                .Where(membership => membership.OrganizationId == organizationId)
                .OrderBy(membership => membership.Role)    // Owner = 0 comes before Member = 1
                .ThenBy(membership => membership.JoinedAt)
                .ThenBy(membership => membership.User.DisplayName)
                .Select(membership => new MemberDto
                {
                    UserId = membership.UserId,
                    DisplayName = membership.User.DisplayName,
                    Email = membership.User.Email!,
                    Role = membership.Role,
                    JoinedAt = membership.JoinedAt
                })
                .ToListAsync();
        }

        public async Task<MemberOperationResult> AddAsync(string userId, int organizationId, string email)
        {
            var mine = await FindMembershipAsync(userId, organizationId);

            if (mine == null)
                return Fail(MemberOperationStatus.OrganizationNotFound);

            if (mine.Organization.IsDemo)
                return Fail(MemberOperationStatus.DemoLocked);

            if (mine.Role != OrganizationRole.Owner)
                return Fail(MemberOperationStatus.Forbidden);

            // Identity keeps a normalized (uppercase) copy of every email, which is what lookups use
            var normalizedEmail = email.Trim().ToUpperInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

            if (user == null)
                return Fail(MemberOperationStatus.UserNotFound);

            if (await IsMemberAsync(user.Id, organizationId))
                return Fail(MemberOperationStatus.AlreadyMember);

            var membership = new Membership
            {
                OrganizationId = organizationId,
                UserId = user.Id,
                Role = OrganizationRole.Member,
                JoinedAt = _clock.GetUtcNow()
            };
            _context.Memberships.Add(membership);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsDuplicateKey())
            {
                // Two requests added the same person at once: the primary key stopped the second
                return Fail(MemberOperationStatus.AlreadyMember);
            }

            return new MemberOperationResult
            {
                Status = MemberOperationStatus.Success,
                Member = new MemberDto
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email!,
                    Role = membership.Role,
                    JoinedAt = membership.JoinedAt
                }
            };
        }

        public async Task<MemberOperationResult> RemoveAsync(string userId, int organizationId, string memberUserId)
        {
            var mine = await FindMembershipAsync(userId, organizationId);

            if (mine == null)
                return Fail(MemberOperationStatus.OrganizationNotFound);

            if (mine.Organization.IsDemo)
                return Fail(MemberOperationStatus.DemoLocked);

            var target = await _context.Memberships
                .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == memberUserId);

            if (target == null)
                return Fail(MemberOperationStatus.MemberNotFound);

            // The Owner can remove anybody; everybody else can only remove themselves (leave)
            var isLeaving = memberUserId == userId;
            if (mine.Role != OrganizationRole.Owner && !isLeaving)
                return Fail(MemberOperationStatus.Forbidden);

            if (target.Role == OrganizationRole.Owner)
                return Fail(MemberOperationStatus.OwnerCannotBeRemoved);

            _context.Memberships.Remove(target);
            await _context.SaveChangesAsync();

            return new MemberOperationResult { Status = MemberOperationStatus.Success };
        }

        private Task<Membership?> FindMembershipAsync(string userId, int organizationId)
        {
            return _context.Memberships
                .Include(membership => membership.Organization)
                .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
        }

        private Task<bool> IsMemberAsync(string userId, int organizationId)
        {
            return _context.Memberships
                .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
        }

        private static MemberOperationResult Fail(MemberOperationStatus status)
            => new() { Status = status };
    }
}
