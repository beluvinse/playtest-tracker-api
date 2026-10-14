using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Options;

namespace PlaytestTracker.Api.Services
{
    // Tells the outside world about the demo, so the sign-in page can show the demo accounts
    // without having them written in the frontend's code
    public class DemoService
    {
        private readonly AppDbContext _context;
        private readonly IOptions<DemoOptions> _options;

        public DemoService(AppDbContext context, IOptions<DemoOptions> options)
        {
            _context = context;
            _options = options;
        }

        // Null when there is no demo to talk about: it is turned off, or it hasn't been created
        public async Task<DemoInfoDto?> GetInfoAsync()
        {
            if (!_options.Value.Enabled)
                return null;

            var demo = await _context.Organizations
                .Where(organization => organization.IsDemo)
                .Select(organization => new { organization.Id, organization.Name })
                .FirstOrDefaultAsync();

            if (demo == null)
                return null;

            return new DemoInfoDto
            {
                OrganizationId = demo.Id,
                OrganizationName = demo.Name,
                Password = _options.Value.Password,

                // Only the accounts made for the demo, never the visitors who joined it
                Accounts = DemoSeeder.People
                    .Select((person, index) => new DemoAccountDto
                    {
                        Email = person.Email,
                        DisplayName = person.DisplayName,
                        Role = index == 0 ? OrganizationRole.Owner : OrganizationRole.Member
                    })
                    .ToList()
            };
        }
    }
}
