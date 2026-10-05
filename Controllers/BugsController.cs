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
        public async Task<ActionResult<PagedResultDto<BugReport>>> GetAll(
            BugStatus? status,
            Severity? severity,
            string? search,
            string? sortBy,
            bool descending = false,
            int page = 1,
            int pageSize = 10)
        {
            var result = await _bugService.GetAllAsync(
                status,
                severity,
                search,
                sortBy,
                descending,
                page,
                pageSize
            );

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BugReport>> GetById(int id)
        {
            var bug = await _bugService.GetByIdAsync(id);

            if (bug == null)
            {
                return NotFound();
            }

            return Ok(bug);
        }

        [HttpPost]
        public async Task<ActionResult<BugReport>> Create(CreateBugDto dto)
        {
            var bug = new BugReport
            {
                ProjectId = dto.ProjectId,
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity
            };

            var createdBug = await _bugService.AddAsync(bug);

            if (createdBug == null)
            {
                return BadRequest("The specified project does not exist.");
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = createdBug.Id },
                createdBug
            );
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BugReport>> Update(
            int id,
            UpdateBugDto dto)
        {
            var updatedBug = new BugReport
            {
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity,
                Status = dto.Status
            };

            var bug = await _bugService.UpdateAsync(id, updatedBug);

            if (bug == null)
            {
                return NotFound();
            }

            return Ok(bug);
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
    }
}
