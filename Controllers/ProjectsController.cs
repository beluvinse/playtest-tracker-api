using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Extensions;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // A single project, by its own id. Listing and creating happen inside an organization
    // (OrganizationProjectsController). Whatever belongs to an organization the person isn't
    // in is answered 404, exactly as if it didn't exist.
    // Everything here needs a valid token: without one, the request is answered 401 before any code runs
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly ProjectService _projectService;

        public ProjectsController(ProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetById(int id)
        {
            var project = await _projectService.GetByIdAsync(User.GetUserId(), id);

            if (project == null)
                return NotFound();

            return Ok(project);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ProjectDto>> Update(
            int id,
            UpdateProjectDto dto)
        {
            var updatedProject = new Project
            {
                Name = dto.Name,
                Code = dto.Code.ToUpperInvariant(),
                Description = dto.Description
            };

            var result = await _projectService.UpdateAsync(User.GetUserId(), id, updatedProject);

            return result.Status switch
            {
                ProjectOperationStatus.ProjectNotFound => NotFound(),
                ProjectOperationStatus.CodeTaken => this.CodeTaken(updatedProject.Code),
                _ => Ok(result.Project)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _projectService.DeleteAsync(User.GetUserId(), id);

            return result switch
            {
                DeleteProjectResult.NotFound => NotFound(),
                DeleteProjectResult.Forbidden => Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Only the owner can delete projects",
                    detail: "Ask the owner of the organization to delete this project."),
                DeleteProjectResult.HasBugs => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Project has bugs",
                    detail: "Delete or move this project's bugs before deleting it."),
                _ => NoContent()
            };
        }
    }
}
