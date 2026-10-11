using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // Bug operations that happen inside a project. Single-bug operations stay on /api/bugs/{id}.
    // Everything here needs a valid token: without one, the request is answered 401 before any code runs
    [Authorize]
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

        [HttpPost]
        public async Task<ActionResult<BugDto>> Create(int projectId, CreateBugDto dto)
        {
            var bug = new BugReport
            {
                ProjectId = projectId,
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity
            };

            var result = await _bugService.AddAsync(bug);

            if (result.Status == BugOperationStatus.ProjectNotFound)
                return NotFound();

            // The new bug lives at /api/bugs/{id}, like every single-bug operation
            return CreatedAtAction(
                nameof(BugsController.GetById),
                "Bugs",
                new { id = result.Bug!.Id },
                result.Bug
            );
        }

        // DELETE on the collection empties it: every bug of the project goes, the project stays.
        // Meant for clearing a project before deleting it, which the API refuses while it has bugs.
        [HttpDelete]
        public async Task<IActionResult> DeleteAll(int projectId)
        {
            var deletedCount = await _bugService.DeleteAllInProjectAsync(projectId);

            if (deletedCount == null)
                return NotFound();

            return NoContent();
        }
    }
}
