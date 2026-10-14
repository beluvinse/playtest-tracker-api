using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    // Every method takes the id of the person asking, and only sees the projects of the
    // organizations that person belongs to. What is out of reach is answered as "not found",
    // the same as something that doesn't exist, so nobody can tell whether it is there.
    public class ProjectService
    {
        private static readonly Expression<Func<Project, ProjectDto>> ToDto =
            project => new ProjectDto
            {
                Id = project.Id,
                OrganizationId = project.OrganizationId,
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

        // The projects of one organization. Null when the person isn't a member of it.
        public async Task<List<ProjectDto>?> GetAllAsync(string userId, int organizationId)
        {
            if (!await IsMemberAsync(userId, organizationId))
                return null;

            return await _context.Projects
                .Where(project => project.OrganizationId == organizationId)
                .OrderByDescending(project => project.CreatedAt)
                .Select(ToDto)
                .ToListAsync();
        }

        public async Task<ProjectDto?> GetByIdAsync(string userId, int id)
        {
            return await _context.Projects
                .VisibleTo(userId)
                .Where(project => project.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ExistsAsync(string userId, int id)
        {
            return await _context.Projects.VisibleTo(userId).AnyAsync(project => project.Id == id);
        }

        public async Task<ProjectOperationResult> AddAsync(string userId, int organizationId, Project project)
        {
            if (!await IsMemberAsync(userId, organizationId))
                return new ProjectOperationResult { Status = ProjectOperationStatus.OrganizationNotFound };

            if (await IsCodeTakenAsync(organizationId, project.Code))
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            project.OrganizationId = organizationId;
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

        public async Task<ProjectOperationResult> UpdateAsync(string userId, int id, Project updatedProject)
        {
            // FindAsync looks a project up by id and nothing else, so it would find any person's
            // project. Going through VisibleTo keeps the answer inside the person's organizations.
            var project = await _context.Projects
                .VisibleTo(userId)
                .FirstOrDefaultAsync(project => project.Id == id);

            if (project == null)
                return new ProjectOperationResult { Status = ProjectOperationStatus.ProjectNotFound };

            // Keeping its own code is fine; taking another project's code (in the same organization) is not
            if (await IsCodeTakenAsync(project.OrganizationId, updatedProject.Code, exceptProjectId: id))
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            project.Name = updatedProject.Name;
            project.Code = updatedProject.Code;
            project.Description = updatedProject.Description;

            if (!await TrySaveAsync())
                return new ProjectOperationResult { Status = ProjectOperationStatus.CodeTaken };

            return new ProjectOperationResult
            {
                Status = ProjectOperationStatus.Success,
                Project = await GetByIdAsync(userId, id)
            };
        }

        // Codes are unique inside an organization: two organizations can both have a "PIN"
        private async Task<bool> IsCodeTakenAsync(int organizationId, string code, int? exceptProjectId = null)
        {
            return await _context.Projects
                .AnyAsync(project => project.OrganizationId == organizationId
                    && project.Code == code
                    && project.Id != exceptProjectId);
        }

        private async Task<bool> IsMemberAsync(string userId, int organizationId)
        {
            return await _context.Memberships
                .AnyAsync(member => member.OrganizationId == organizationId && member.UserId == userId);
        }

        // The check above covers the usual case. But if two requests with the same code arrive
        // at the same time, both pass it; the unique index then stops the second one, and the
        // database answers with a "duplicate key" error. That's the same "code taken"
        // situation, so it becomes the same 409 instead of a 500.
        private async Task<bool> TrySaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException exception) when (exception.IsDuplicateKey())
            {
                return false;
            }
        }

        // Only the Owner of the organization can delete one of its projects
        public async Task<DeleteProjectResult> DeleteAsync(string userId, int id)
        {
            var project = await _context.Projects
                .VisibleTo(userId)
                .FirstOrDefaultAsync(project => project.Id == id);

            if (project == null)
                return DeleteProjectResult.NotFound;

            var role = await _context.Memberships
                .Where(member => member.OrganizationId == project.OrganizationId && member.UserId == userId)
                .Select(member => member.Role)
                .SingleAsync();

            if (role != OrganizationRole.Owner)
                return DeleteProjectResult.Forbidden;

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
