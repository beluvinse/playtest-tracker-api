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

        public async Task<PagedResultDto<BugDto>> GetAllAsync(
            int? projectId = null,
            BugStatus? status = null,
            Severity? severity = null,
            string? search = null,
            string? sortBy = null,
            bool descending = false,
            int page = 1,
            int pageSize = 10)
        {
            var query = _context.Bugs.AsQueryable();

            if (projectId.HasValue)
                query = query.Where(bug => bug.ProjectId == projectId.Value);

            if (status.HasValue)
                query = query.Where(bug => bug.Status == status.Value);

            if (severity.HasValue)
                query = query.Where(bug => bug.Severity == severity.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(bug =>
                    bug.Title.Contains(search) ||
                    bug.Description.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "createdat" => descending
                        ? query.OrderByDescending(b => b.CreatedAt)
                        : query.OrderBy(b => b.CreatedAt),

                    "severity" => descending
                        ? query.OrderByDescending(b => b.Severity)
                        : query.OrderBy(b => b.Severity),

                    "status" => descending
                        ? query.OrderByDescending(b => b.Status)
                        : query.OrderBy(b => b.Status),

                    _ => query
                };
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDto)
                .ToListAsync();

            return new PagedResultDto<BugDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<BugDto?> GetByIdAsync(int id)
        {
            return await _context.Bugs
                .Where(bug => bug.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();
        }

        public async Task<BugOperationResult> AddAsync(BugReport bug)
        {
            if (!await ProjectExistsAsync(bug.ProjectId))
                return new BugOperationResult { Status = BugOperationStatus.ProjectNotFound };

            bug.Status = BugStatus.Open;
            bug.CreatedAt = DateTime.Now;

            _context.Bugs.Add(bug);
            await _context.SaveChangesAsync();

            return new BugOperationResult
            {
                Status = BugOperationStatus.Success,
                Bug = await GetByIdAsync(bug.Id)
            };
        }

        public async Task<BugOperationResult> UpdateAsync(int id, BugReport updatedBug)
        {
            var bug = await _context.Bugs.FindAsync(id);

            if (bug == null)
                return new BugOperationResult { Status = BugOperationStatus.BugNotFound };

            if (!await ProjectExistsAsync(updatedBug.ProjectId))
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
                Bug = await GetByIdAsync(id)
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var bug = await _context.Bugs.FindAsync(id);

            if (bug == null)
                return false;

            _context.Bugs.Remove(bug);
            await _context.SaveChangesAsync();

            return true;

        }

        private async Task<bool> ProjectExistsAsync(int? projectId)
        {
            if (!projectId.HasValue)
                return true;

            return await _context.Projects
                .AnyAsync(project => project.Id == projectId.Value);
        }
    }
}
