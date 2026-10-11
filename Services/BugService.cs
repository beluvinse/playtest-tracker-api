using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    public class BugService
    {
        private static readonly Expression<Func<BugReport, BugDto>> ToDto =
            bug => new BugDto
            {
                Id = bug.Id,
                ProjectId = bug.ProjectId,
                Title = bug.Title,
                Description = bug.Description,
                Severity = bug.Severity,
                Status = bug.Status,
                CreatedAt = bug.CreatedAt
            };

        private readonly AppDbContext _context;

        public BugService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResultDto<BugDto>> GetAllAsync(string userId, BugListParameters parameters, int? projectId = null)
        {
            var query = _context.Bugs.VisibleTo(userId);

            if (projectId.HasValue)
                query = query.Where(bug => bug.ProjectId == projectId.Value);

            if (parameters.Status.HasValue)
                query = query.Where(bug => bug.Status == parameters.Status.Value);

            if (parameters.Severity.HasValue)
                query = query.Where(bug => bug.Severity == parameters.Severity.Value);

            if (!string.IsNullOrWhiteSpace(parameters.Search))
            {
                query = query.Where(bug =>
                    bug.Title.Contains(parameters.Search) ||
                    bug.Description.Contains(parameters.Search));
            }

            var descending = parameters.Descending;

            IOrderedQueryable<BugReport> orderedQuery = parameters.SortBy switch
            {
                BugSortField.CreatedAt => descending
                    ? query.OrderByDescending(b => b.CreatedAt)
                    : query.OrderBy(b => b.CreatedAt),

                BugSortField.Severity => descending
                    ? query.OrderByDescending(b => b.Severity)
                    : query.OrderBy(b => b.Severity),

                BugSortField.Status => descending
                    ? query.OrderByDescending(b => b.Status)
                    : query.OrderBy(b => b.Status),

                // Without sortBy, newest bugs come first
                _ => query.OrderByDescending(b => b.CreatedAt)
            };

            // Id is unique, so ties never shuffle between pages
            query = orderedQuery.ThenBy(b => b.Id);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(ToDto)
                .ToListAsync();

            return new PagedResultDto<BugDto>
            {
                Items = items,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<BugDto?> GetByIdAsync(string userId, int id)
        {
            return await _context.Bugs
                .VisibleTo(userId)
                .Where(bug => bug.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();
        }

        public async Task<BugOperationResult> AddAsync(string userId, BugReport bug)
        {
            if (!await ProjectExistsAsync(userId, bug.ProjectId))
                return new BugOperationResult { Status = BugOperationStatus.ProjectNotFound };

            bug.Status = BugStatus.Open;
            bug.CreatedAt = DateTimeOffset.UtcNow;

            _context.Bugs.Add(bug);
            await _context.SaveChangesAsync();

            return new BugOperationResult
            {
                Status = BugOperationStatus.Success,
                Bug = await GetByIdAsync(userId, bug.Id)
            };
        }

        public async Task<BugOperationResult> UpdateAsync(string userId, int id, BugReport updatedBug)
        {
            var bug = await FindVisibleAsync(userId, id);

            if (bug == null)
                return new BugOperationResult { Status = BugOperationStatus.BugNotFound };

            if (!await CanMoveToProjectAsync(userId, bug, updatedBug.ProjectId))
                return new BugOperationResult { Status = BugOperationStatus.ProjectNotFound };

            bug.ProjectId = updatedBug.ProjectId;
            bug.Title = updatedBug.Title;
            bug.Description = updatedBug.Description;
            bug.Severity = updatedBug.Severity;
            bug.Status = updatedBug.Status;

            await _context.SaveChangesAsync();

            return new BugOperationResult
            {
                Status = BugOperationStatus.Success,
                Bug = await GetByIdAsync(userId, id)
            };
        }

        // Receives the DTO instead of an entity: an entity can't tell "not sent" apart from a real value
        public async Task<BugOperationResult> PatchAsync(string userId, int id, PatchBugDto changes)
        {
            var bug = await FindVisibleAsync(userId, id);

            if (bug == null)
                return new BugOperationResult { Status = BugOperationStatus.BugNotFound };

            if (changes.ProjectId.HasValue && !await CanMoveToProjectAsync(userId, bug, changes.ProjectId.Value))
                return new BugOperationResult { Status = BugOperationStatus.ProjectNotFound };

            if (changes.ProjectId.HasValue)
                bug.ProjectId = changes.ProjectId.Value;

            if (changes.Title != null)
                bug.Title = changes.Title;

            if (changes.Description != null)
                bug.Description = changes.Description;

            if (changes.Severity.HasValue)
                bug.Severity = changes.Severity.Value;

            if (changes.Status.HasValue)
                bug.Status = changes.Status.Value;

            await _context.SaveChangesAsync();

            return new BugOperationResult
            {
                Status = BugOperationStatus.Success,
                Bug = await GetByIdAsync(userId, id)
            };
        }

        public async Task<bool> DeleteAsync(string userId, int id)
        {
            var bug = await FindVisibleAsync(userId, id);

            if (bug == null)
                return false;

            _context.Bugs.Remove(bug);
            await _context.SaveChangesAsync();

            return true;

        }

        // Deletes every bug of a project with a single SQL statement:
        //   DELETE FROM Bugs WHERE ProjectId = @projectId
        // ExecuteDeleteAsync runs it straight in the database, without loading the bugs
        // into memory first (Remove + SaveChanges would load them all, then delete one by one).
        // Returns null when the project doesn't exist, otherwise how many bugs were deleted.
        public async Task<int?> DeleteAllInProjectAsync(string userId, int projectId)
        {
            if (!await ProjectExistsAsync(userId, projectId))
                return null;

            return await _context.Bugs
                .Where(bug => bug.ProjectId == projectId)
                .ExecuteDeleteAsync();
        }

        // A project the person can see (it belongs to one of their organizations)
        private async Task<bool> ProjectExistsAsync(string userId, int projectId)
        {
            return await _context.Projects
                .VisibleTo(userId)
                .AnyAsync(project => project.Id == projectId);
        }

        // FindAsync would find any bug by its id, whoever it belongs to. This only finds
        // the ones in the person's organizations.
        private async Task<BugReport?> FindVisibleAsync(string userId, int id)
        {
            return await _context.Bugs
                .VisibleTo(userId)
                .Include(bug => bug.Project)
                .FirstOrDefaultAsync(bug => bug.Id == id);
        }

        // A bug can move to another project, but only inside its own organization: moving it
        // to a project of a different organization would hand it over to other people.
        // For anything else (missing, someone else's, another organization's) the answer is the same.
        private async Task<bool> CanMoveToProjectAsync(string userId, BugReport bug, int targetProjectId)
        {
            return await _context.Projects
                .VisibleTo(userId)
                .AnyAsync(project => project.Id == targetProjectId
                    && project.OrganizationId == bug.Project.OrganizationId);
        }
    }
}
