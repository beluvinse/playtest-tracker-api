using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
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
        public async Task<ActionResult<List<Project>>> GetAll()
        {
            var projects = await _projectService.GetAllAsync();

            return Ok(projects);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetById(int id)
        {
            var project = await _projectService.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            return Ok(project);
        }

        [HttpPost]
        public async Task<ActionResult<Project>> Create(CreateProjectDto dto)
        {
            var project = new Project
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var createdProject = await _projectService.AddAsync(project);

            return CreatedAtAction(
                nameof(GetById),
                new { id = createdProject.Id },
                createdProject
            );
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Project>> Update(
            int id,
            CreateProjectDto dto)
        {
            var updatedProject = new Project
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var project = await _projectService.UpdateAsync(id, updatedProject);

            if (project == null)
                return NotFound();

            return Ok(project);
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
