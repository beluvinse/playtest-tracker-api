using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlaytestTracker.Api.Options;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // Chores for a scheduled job (a GitHub Actions workflow), not for people. It has no sign-in
    // (nobody is "logged in" in a cron job) so it is protected by a secret key instead, sent in the
    // X-Maintenance-Key header.
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceController : ControllerBase
    {
        private readonly DemoCleaner _cleaner;
        private readonly IOptions<MaintenanceOptions> _options;

        public MaintenanceController(DemoCleaner cleaner, IOptions<MaintenanceOptions> options)
        {
            _cleaner = cleaner;
            _options = options;
        }

        [HttpPost("clean-demo")]
        public async Task<ActionResult<CleanupResult>> CleanDemo(
            [FromHeader(Name = "X-Maintenance-Key")] string? key)
        {
            var expected = _options.Value.Key;

            // No (valid) key configured: as far as the outside is concerned, this doesn't exist
            if (expected.Length < MaintenanceOptions.MinKeyLength)
                return NotFound();

            if (string.IsNullOrEmpty(key) || !SameKey(key, expected))
                return Unauthorized();

            return Ok(await _cleaner.CleanAsync());
        }

        // Compares in constant time: a normal == stops at the first different letter, and how long
        // it took would hint at how many letters were right. Hashing both first gives them the
        // same length, so even that can't leak.
        private static bool SameKey(string given, string expected) =>
            CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(given)),
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}
