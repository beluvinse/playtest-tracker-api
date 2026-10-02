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
    }
}
