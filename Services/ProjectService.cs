using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    public class ProjectService
    {
        private readonly AppDbContext _context;

        public ProjectService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Project>> GetAllAsync()
        {
            return await _context.Projects
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync();
        }

        public async Task<Project?> GetByIdAsync(int id)
        {
            return await _context.Projects.FindAsync(id);
        }

        public async Task<Project> AddAsync(Project project)
        {
            project.CreatedAt = DateTime.Now;

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return project;
        }

        public async Task<Project?> UpdateAsync(int id, Project updatedProject)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return null;

            project.Name = updatedProject.Name;
            project.Description = updatedProject.Description;

            await _context.SaveChangesAsync();

            return project;
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