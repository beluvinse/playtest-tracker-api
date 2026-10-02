using Microsoft.AspNetCore.Mvc;
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
        public ActionResult<List<BugReport>> GetAll()
        {
            return Ok(_bugService.GetAll());
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
        public ActionResult<BugReport> Create(BugReport bug)
        {
            var createdBug = _bugService.Add(bug);

            return CreatedAtAction(
                nameof(GetById),
                new { id = createdBug.Id },
                createdBug
            );
        }

        [HttpPut("{id}")]
        public ActionResult<BugReport> Update(int id, BugReport updatedBug)
        {
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
