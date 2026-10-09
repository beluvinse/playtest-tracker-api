using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BugsController : ControllerBase
    {
        private readonly BugService _bugService;

        public BugsController(BugService bugService)
        {
            _bugService = bugService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResultDto<BugDto>>> GetAll(
            [FromQuery] BugQueryParameters parameters)
        {
            var result = await _bugService.GetAllAsync(parameters);

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BugDto>> GetById(int id)
        {
            var bug = await _bugService.GetByIdAsync(id);

            if (bug == null)
            {
                return NotFound();
            }

            return Ok(bug);
        }

        [HttpPost]
        public async Task<ActionResult<BugDto>> Create(CreateBugDto dto)
        {
            var bug = new BugReport
            {
                ProjectId = dto.ProjectId!.Value,
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity
            };

            var result = await _bugService.AddAsync(bug);

            if (result.Status == BugOperationStatus.ProjectNotFound)
                return ProjectNotFound(nameof(dto.ProjectId));

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Bug!.Id },
                result.Bug
            );
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BugDto>> Update(
            int id,
            UpdateBugDto dto)
        {
            var updatedBug = new BugReport
            {
                ProjectId = dto.ProjectId!.Value,
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity,
                Status = dto.Status
            };

            var result = await _bugService.UpdateAsync(id, updatedBug);

            return result.Status switch
            {
                BugOperationStatus.BugNotFound => NotFound(),
                BugOperationStatus.ProjectNotFound => ProjectNotFound(nameof(dto.ProjectId)),
                _ => Ok(result.Bug)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _bugService.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        private ActionResult ProjectNotFound(string fieldName)
        {
            ModelState.AddModelError(fieldName, "The specified project does not exist.");
            return ValidationProblem(ModelState);
        }
    }
}
