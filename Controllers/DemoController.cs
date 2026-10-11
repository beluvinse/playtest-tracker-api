using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // Public on purpose (no token needed): the sign-in page uses it to show the demo accounts to
    // people who have no account yet. It answers 404 when there is no demo.
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class DemoController : ControllerBase
    {
        private readonly DemoService _demoService;

        public DemoController(DemoService demoService)
        {
            _demoService = demoService;
        }

        [HttpGet]
        public async Task<ActionResult<DemoInfoDto>> Get()
        {
            var info = await _demoService.GetInfoAsync();

            if (info == null)
                return NotFound();

            return Ok(info);
        }
    }
}
