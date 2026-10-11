using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Options;

namespace PlaytestTracker.Api.Services
{
    // Creates the demo organization: an Owner and four Members with public accounts, and two
    // projects with bugs in every state, so somebody who tries the app finds something already
    // there. It is safe to run on every start: if the demo already exists it does nothing.
    public class DemoSeeder
    {
        public const string OrganizationName = "Pink in the Night Studio";

        // The people of the demo. The first one is the Owner.
        public static readonly IReadOnlyList<(string Email, string DisplayName)> People =
        [
            ("luna@demo.test", "Luna Park"),
            ("theo@demo.test", "Theo Vance"),
            ("iris@demo.test", "Iris Okoye"),
            ("nico@demo.test", "Nico Brandt"),
            ("sol@demo.test", "Sol Marin")
        ];

        private readonly AppDbContext _context;
        private readonly AuthService _auth;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<DemoOptions> _options;
        private readonly TimeProvider _clock;
        private readonly ILogger<DemoSeeder> _logger;

        public DemoSeeder(
            AppDbContext context,
            AuthService auth,
            UserManager<ApplicationUser> userManager,
            IOptions<DemoOptions> options,
            TimeProvider clock,
            ILogger<DemoSeeder> logger)
        {
            _context = context;
            _auth = auth;
            _userManager = userManager;
            _options = options;
            _clock = clock;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            if (!_options.Value.Enabled)
                return;

            if (await _context.Organizations.AnyAsync(organization => organization.IsDemo))
                return;

            // The accounts first. Each is a normal sign-up (so each also gets a personal
            // organization). If an earlier attempt got this far, the account already exists.
            var people = new List<ApplicationUser>();
            foreach (var (email, displayName) in People)
                people.Add(await FindOrCreateAccountAsync(email, displayName));

            // Then the organization with everything in it, saved in one go: there is never a
            // half-made demo. The projects are what gets added: each one brings along its
            // organization (with its members) and its bugs.
            _context.Projects.AddRange(BuildDemo(people));
            await _context.SaveChangesAsync();

            _logger.LogInformation("The demo organization was created.");
        }

        private async Task<ApplicationUser> FindOrCreateAccountAsync(string email, string displayName)
        {
            var result = await _auth.RegisterAsync(email, _options.Value.Password, displayName);

            if (result.Status == RegisterStatus.Success)
                return result.User!;

            if (result.Status == RegisterStatus.EmailTaken)
                return (await _userManager.FindByEmailAsync(email))!;

            throw new InvalidOperationException(
                $"Could not create the demo account {email} ({result.Status}). " +
                "Check that Demo:Password meets the password rules.");
        }

        // The demo organization and its projects (returned, because the organization itself has no
        // list of projects: it is reached from them)
        private List<Project> BuildDemo(List<ApplicationUser> people)
        {
            var now = _clock.GetUtcNow();

            var organization = new Organization
            {
                Name = OrganizationName,
                IsDemo = true,
                CreatedAt = now.AddDays(-15)
            };

            // The first person is the Owner, the rest are Members who joined a day apart
            for (var i = 0; i < people.Count; i++)
            {
                organization.Members.Add(new Membership
                {
                    UserId = people[i].Id,
                    Role = i == 0 ? OrganizationRole.Owner : OrganizationRole.Member,
                    JoinedAt = organization.CreatedAt.AddDays(i)
                });
            }

            var pink = NewProject(organization, "Pink in the Night", "PIN", "Seated VR experience", now.AddDays(-14));
            var neon = NewProject(organization, "Neon Drifters", "NEO", "Arcade racing prototype", now.AddDays(-9));

            AddBug(pink, now, 12, Severity.Medium, BugStatus.Open,
                "Neon sign flickers too fast",
                "1. Start a new night\n2. Look at the neon sign\nExpected: slow pulse. Actual: fast flicker.");
            AddBug(pink, now, 11, Severity.Critical, BugStatus.Open,
                "Player falls through the floor in the lobby",
                "Happens when sitting down right after the intro ends. The floor collider is not ready yet.");
            AddBug(pink, now, 10, Severity.Low, BugStatus.Resolved,
                "Comfort vignette doesn't turn off in settings",
                "The toggle changes, but the vignette stays on until the game is restarted.");
            AddBug(pink, now, 9, Severity.High, BugStatus.InProgress,
                "Audio cuts out when taking off the headset",
                "Putting the headset back on does not bring the sound back. Only restarting the game fixes it.");
            AddBug(pink, now, 8, Severity.Medium, BugStatus.Open,
                "Menu laser pointer drifts to the left",
                "After a minute in the menu the pointer is a few centimeters off from where the controller points.");
            AddBug(pink, now, 7, Severity.High, BugStatus.Open,
                "Seat height calibration resets after every night",
                "The calibration is saved, but it is ignored when the next night loads.");
            AddBug(pink, now, 6, Severity.Low, BugStatus.Closed,
                "Subtitles overlap the radio UI",
                "Only with subtitles on and the radio open. Won't fix: the radio is being redesigned.");
            AddBug(pink, now, 4, Severity.Critical, BugStatus.InProgress,
                "Game crashes when loading the second night",
                "Reproduced on the standalone build, never in the editor. Looks like a missing asset in the build.");
            AddBug(pink, now, 2, Severity.Low, BugStatus.Open,
                "Cat NPC walks through the sofa",
                "The cat's path ignores the sofa. Funny, but it breaks the cozy mood.");

            AddBug(neon, now, 8, Severity.High, BugStatus.Open,
                "Boost meter doesn't refill after a crash",
                "After crashing into a wall the meter stays at zero until the next lap.");
            AddBug(neon, now, 7, Severity.Medium, BugStatus.InProgress,
                "Ghost car appears at the wrong lap",
                "The ghost of your best run starts one lap ahead when you restart from the pause menu.");
            AddBug(neon, now, 5, Severity.Critical, BugStatus.Resolved,
                "Finish line triggers twice on tight laps",
                "Lap times are counted twice if the car crosses the line at an angle. Fixed by checking the direction.");
            AddBug(neon, now, 3, Severity.High, BugStatus.Open,
                "Leaderboard shows negative times",
                "Times below zero show up for runs that finish right after a restart.");
            AddBug(neon, now, 1, Severity.Medium, BugStatus.Open,
                "Rain particles tank the framerate",
                "Dropping from 90 to 45 fps on the rainy track, only on the older test device.");

            return [pink, neon];
        }

        private static Project NewProject(
            Organization organization, string name, string code, string description, DateTimeOffset createdAt)
        {
            var project = new Project
            {
                Name = name,
                Code = code,
                Description = description,
                CreatedAt = createdAt,
                Organization = organization
            };
            return project;
        }

        private static void AddBug(
            Project project, DateTimeOffset now, int daysAgo,
            Severity severity, BugStatus status, string title, string description)
        {
            project.Bugs.Add(new BugReport
            {
                Title = title,
                Description = description,
                Severity = severity,
                Status = status,
                CreatedAt = now.AddDays(-daysAgo)
            });
        }
    }
}
