using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    public class BugService
    {
        private readonly AppDbContext _context;

        public BugService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResultDto<BugReport>> GetAllAsync(
            BugStatus? status = null,
            Severity? severity = null,
            string? search = null,
            string? sortBy = null,
            bool descending = false,
            int page = 1,
            int pageSize = 10)
        {
            var query = _context.Bugs.AsQueryable();

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
                .ToListAsync();

            return new PagedResultDto<BugReport>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<BugReport?> GetByIdAsync(int id)
        {
            return await _context.Bugs.FindAsync(id);
        }

        public async Task<BugReport> AddAsync(BugReport bug)
        {
            bug.Status = BugStatus.Open;
            bug.CreatedAt = DateTime.Now;

            _context.Bugs.Add(bug);
            await _context.SaveChangesAsync();

            return bug;
        }

        public async Task<BugReport?> UpdateAsync(int id, BugReport updatedBug)
        {
            var bug = await _context.Bugs.FindAsync(id);

            if (bug == null)
                return null;

            bug.Title = updatedBug.Title;
            bug.Description = updatedBug.Description;
            bug.Severity = updatedBug.Severity;
            bug.Status = updatedBug.Status;

            await _context.SaveChangesAsync();

            return bug;
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
    }
}
