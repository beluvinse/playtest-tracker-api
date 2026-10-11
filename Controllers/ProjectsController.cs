using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
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

        [HttpGet]
        public async Task<ActionResult<List<ProjectDto>>> GetAll()
        {
            var projects = await _projectService.GetAllAsync();

            return Ok(projects);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetById(int id)
        {
            var project = await _projectService.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            return Ok(project);
        }

        [HttpPost]
        public async Task<ActionResult<ProjectDto>> Create(CreateProjectDto dto)
        {
            var project = new Project
            {
                Name = dto.Name,
                Code = dto.Code.ToUpperInvariant(),
                Description = dto.Description
            };

            var result = await _projectService.AddAsync(project);

            if (result.Status == ProjectOperationStatus.CodeTaken)
                return CodeTaken(project.Code);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Project!.Id },
                result.Project
            );
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

            var result = await _projectService.UpdateAsync(id, updatedProject);

            return result.Status switch
            {
                ProjectOperationStatus.ProjectNotFound => NotFound(),
                ProjectOperationStatus.CodeTaken => CodeTaken(updatedProject.Code),
                _ => Ok(result.Project)
            };
        }

        // 409 Conflict with the message attached to the "Code" field, in the same "errors"
        // format as a validation error, so a form can show it right under the code input
        private ActionResult CodeTaken(string code)
        {
            ModelState.AddModelError(
                nameof(ProjectRequestDto.Code),
                $"Another project already uses the code {code}.");

            return ValidationProblem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Code already in use",
                modelStateDictionary: ModelState);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _projectService.DeleteAsync(id);

            return result switch
            {
                DeleteProjectResult.NotFound => NotFound(),
                DeleteProjectResult.HasBugs => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Project has bugs",
                    detail: "Delete or move this project's bugs before deleting it."),
                _ => NoContent()
            };
        }
    }

}
