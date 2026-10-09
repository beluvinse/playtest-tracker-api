using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // Bug operations that happen inside a project. Single-bug operations stay on /api/bugs/{id}.
    [ApiController]
    [Route("api/projects/{projectId}/bugs")]
    public class ProjectBugsController : ControllerBase
    {
        private readonly BugService _bugService;
        private readonly ProjectService _projectService;

        public ProjectBugsController(BugService bugService, ProjectService projectService)
        {
            _bugService = bugService;
            _projectService = projectService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResultDto<BugDto>>> GetAll(
            int projectId,
            [FromQuery] BugListParameters parameters)
        {
            if (!await _projectService.ExistsAsync(projectId))
                return NotFound();

            var result = await _bugService.GetAllAsync(parameters, projectId);

            return Ok(result);
        }
    }
}
