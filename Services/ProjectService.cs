using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    public class ProjectService
    {
        private static readonly Expression<Func<Project, ProjectDto>> ToDto =
            project => new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Code = project.Code,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                BugCount = project.Bugs.Count(),
                // Both counts are computed in the same SQL query as the project itself.
                // "Open" here means not finished yet: Open or InProgress (Resolved and Closed are done).
                OpenCriticalCount = project.Bugs.Count(bug =>
                    bug.Severity == Severity.Critical &&
                    (bug.Status == BugStatus.Open || bug.Status == BugStatus.InProgress))
            };

        private static readonly Func<Project, ProjectDto> MapToDto = ToDto.Compile();

        private readonly AppDbContext _context;

        public ProjectService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProjectDto>> GetAllAsync()
        {
            return await _context.Projects
                .OrderByDescending(project => project.CreatedAt)
                .Select(ToDto)
                .ToListAsync();
        }

        public async Task<ProjectDto?> GetByIdAsync(int id)
        {
            return await _context.Projects
                .Where(project => project.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Projects.AnyAsync(project => project.Id == id);
        }

        public async Task<ProjectOperationResult> AddAsync(Project project)
        {
            if (await IsCodeTakenAsync(project.Code))
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            project.CreatedAt = DateTimeOffset.UtcNow;
            _context.Projects.Add(project);

            if (!await TrySaveAsync())
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            // A new project has no bugs yet, so the in-memory entity already has everything the DTO needs
            return new ProjectOperationResult
            {
                Status = ProjectOperationStatus.Success,
                Project = MapToDto(project)
            };
        }

        public async Task<ProjectOperationResult> UpdateAsync(int id, Project updatedProject)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return new ProjectOperationResult { Status = ProjectOperationStatus.ProjectNotFound };

            // Keeping its own code is fine; taking another project's code is not
            if (await IsCodeTakenAsync(updatedProject.Code, exceptProjectId: id))
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            project.Name = updatedProject.Name;
            project.Code = updatedProject.Code;
            project.Description = updatedProject.Description;

            if (!await TrySaveAsync())
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            return new ProjectOperationResult
            {
                Status = ProjectOperationStatus.Success,
                Project = await GetByIdAsync(id)
            };
        }

        private async Task<bool> IsCodeTakenAsync(string code, int? exceptProjectId = null)
        {
            return await _context.Projects
                .AnyAsync(project => project.Code == code && project.Id != exceptProjectId);
        }

        // The check above covers the usual case. But if two requests with the same code arrive
        // at the same time, both pass it; the unique index then stops the second one, and
        // SQL Server answers with error 2601 ("duplicate key"). That's the same "code taken"
        // situation, so it becomes the same 409 instead of a 500.
        private async Task<bool> TrySaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return false;
            }
        }

        public async Task<DeleteProjectResult> DeleteAsync(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return DeleteProjectResult.NotFound;

            var hasBugs = await _context.Bugs
                .AnyAsync(bug => bug.ProjectId == id);

            if (hasBugs)
                return DeleteProjectResult.HasBugs;

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return DeleteProjectResult.Deleted;
        }
    }
}
