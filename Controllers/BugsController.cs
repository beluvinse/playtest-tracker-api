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
        public ActionResult<PagedResultDto<BugReport>> GetAll(
            BugStatus? status,
            Severity? severity,
            string? search,
            string? sortBy,
            bool descending = false,
            int page = 1,
            int pageSize = 10)
        {
            return Ok(
                _bugService.GetAll(
                    status,
                    severity,
                    search,
                    sortBy,
                    descending,
                    page,
                    pageSize
                )
            );
        }

        [HttpGet("{id}")]
        public ActionResult<BugReport> GetById(int id)
        {
            var bug = _bugService.GetById(id);

            if (bug == null)
            {
                return NotFound();
            }

            return Ok(bug);
        }

        [HttpPost]
        public ActionResult<BugReport> Create(CreateBugDto dto)
        {
            var bug = new BugReport
            {
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity
            };

            var createdBug = _bugService.Add(bug);

            return CreatedAtAction(
                nameof(GetById),
                new { id = createdBug.Id },
                createdBug
            );
        }

        [HttpPut("{id}")]
        public ActionResult<BugReport> Update(int id, UpdateBugDto dto)
        {
            var updatedBug = new BugReport
            {
                Title = dto.Title,
                Description = dto.Description,
                Severity = dto.Severity,
                Status = dto.Status
            };

            var bug = _bugService.Update(id, updatedBug);

            if (bug == null)
            {
                return NotFound();
            }

            return Ok(bug);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var deleted = _bugService.Delete(id);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
