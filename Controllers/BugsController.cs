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
            var result = await _bugService.GetAllAsync(parameters, parameters.ProjectId);

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

        [HttpPatch("{id}")]
        public async Task<ActionResult<BugDto>> Patch(int id, PatchBugDto dto)
        {
            var result = await _bugService.PatchAsync(id, dto);

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
