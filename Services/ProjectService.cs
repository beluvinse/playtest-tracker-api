using System.Linq.Expressions;
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
                BugCount = project.Bugs.Count()
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

        public async Task<ProjectDto> AddAsync(Project project)
        {
            project.CreatedAt = DateTimeOffset.UtcNow;

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // A new project has no bugs yet, so the in-memory entity already has everything the DTO needs
            return MapToDto(project);
        }

        public async Task<ProjectDto?> UpdateAsync(int id, Project updatedProject)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return null;

            project.Name = updatedProject.Name;
            project.Code = updatedProject.Code;
            project.Description = updatedProject.Description;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(id);
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
