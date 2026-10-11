using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Extensions;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // The projects of one organization: listing them and creating a new one. A project lives
    // inside an organization, so that is where it is created. After that it is reached by its
    // own id, at /api/projects/{id}.
    // Everything here needs a valid token: without one, the request is answered 401 before any code runs
    [Authorize]
    [ApiController]
    [Route("api/organizations/{organizationId}/projects")]
    public class OrganizationProjectsController : ControllerBase
    {
        private readonly ProjectService _projectService;

        public OrganizationProjectsController(ProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ProjectDto>>> GetAll(int organizationId)
        {
            var projects = await _projectService.GetAllAsync(User.GetUserId(), organizationId);

            // Not a member of that organization (or it doesn't exist)
            if (projects == null)
                return NotFound();

            return Ok(projects);
        }

        [HttpPost]
        public async Task<ActionResult<ProjectDto>> Create(int organizationId, CreateProjectDto dto)
        {
            var project = new Project
            {
                Name = dto.Name,
                Code = dto.Code.ToUpperInvariant(),
                Description = dto.Description
            };

            var result = await _projectService.AddAsync(User.GetUserId(), organizationId, project);

            return result.Status switch
            {
                ProjectOperationStatus.OrganizationNotFound => NotFound(),
                ProjectOperationStatus.CodeTaken => this.CodeTaken(project.Code),
                _ => CreatedAtAction(
                    nameof(ProjectsController.GetById),
                    "Projects",
                    new { id = result.Project!.Id },
                    result.Project)
            };
        }
    }
}
