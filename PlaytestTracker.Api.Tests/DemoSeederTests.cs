using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// What the demo organization is made of, and that creating it again changes nothing
public class DemoSeederTests : IDisposable
{
    private readonly TestApp _app;
    private readonly AppDbContext _db;
    private readonly DemoSeeder _seeder;



    public DemoSeederTests()
    {
        _app = new TestApp(demo: true);
        _db = _app.Get<AppDbContext>();
        _seeder = _app.Get<DemoSeeder>();
    }

    public void Dispose() => _app.Dispose();

    private Task<Organization> TheDemo() => _db.Organizations.SingleAsync(o => o.IsDemo);

    [Fact]
    public async Task Seeding_CreatesTheDemoOrganization_WithOneOwnerAndFourMembers()
    {
        await _seeder.SeedAsync();

        var demo = await TheDemo();
        Assert.Equal(DemoSeeder.OrganizationName, demo.Name);

        var members = await _db.Memberships
            .Where(m => m.OrganizationId == demo.Id)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ToListAsync();
        Assert.Equal(5, members.Count);
        var owner = Assert.Single(members, m => m.Role == OrganizationRole.Owner);
        Assert.Equal("luna@demo.test", owner.User.Email);
        Assert.Equal(4, members.Count(m => m.Role == OrganizationRole.Member));
    }

    [Fact]
    public async Task Seeding_CreatesTwoProjectsWithBugsInEveryState()
    {
        await _seeder.SeedAsync();
        var demo = await TheDemo();

        var projects = await _db.Projects.Where(p => p.OrganizationId == demo.Id).ToListAsync();
        Assert.Equal(["NEO", "PIN"], projects.Select(p => p.Code).Order());

        var bugs = await _db.Bugs.Where(b => b.Project.OrganizationId == demo.Id).ToListAsync();
        Assert.Equal(14, bugs.Count);
        // Enough variety for the filters and counters to have something to show
        Assert.Equal(4, bugs.Select(b => b.Status).Distinct().Count());
        Assert.Equal(4, bugs.Select(b => b.Severity).Distinct().Count());
        Assert.Contains(bugs, b => b.Severity == Severity.Critical && b.Status is BugStatus.Open or BugStatus.InProgress);
        Assert.All(bugs, b => Assert.NotEqual(default, b.CreatedAt));
    }

    [Fact]
    public async Task EveryoneInTheDemo_CanSignInWithTheDemoPassword()
    {
        await _seeder.SeedAsync();
        var auth = _app.Get<AuthService>();

        foreach (var (email, _) in DemoSeeder.People)
        {
            var result = await auth.CheckCredentialsAsync(email, TestApp.DemoPassword);
            Assert.Equal(LoginStatus.Success, result.Status);
        }
    }

    [Fact]
    public async Task EveryDemoAccount_AlsoHasAPersonalOrganization_LikeAnyoneWhoSignsUp()
    {
        await _seeder.SeedAsync();

        // 5 personal ones and the demo
        Assert.Equal(6, await _db.Organizations.CountAsync());
        Assert.Equal(5, await _db.Organizations.CountAsync(o => !o.IsDemo));
    }

    [Fact]
    public async Task Seeding_ASecondTime_ChangesNothing()
    {
        await _seeder.SeedAsync();
        var organizations = await _db.Organizations.CountAsync();
        var memberships = await _db.Memberships.CountAsync();
        var projects = await _db.Projects.CountAsync();
        var bugs = await _db.Bugs.CountAsync();
        var users = await _db.Users.CountAsync();

        await _seeder.SeedAsync();
        await _seeder.SeedAsync();

        Assert.Equal(organizations, await _db.Organizations.CountAsync());
        Assert.Equal(memberships, await _db.Memberships.CountAsync());
        Assert.Equal(projects, await _db.Projects.CountAsync());
        Assert.Equal(bugs, await _db.Bugs.CountAsync());
        Assert.Equal(users, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task Seeding_AfterTheDemoWasLost_BuildsItAgainWithoutDuplicatingTheAccounts()
    {
        await _seeder.SeedAsync();
        // The demo organization disappears (here, deleted directly in the database)
        var demo = await TheDemo();
        await _db.Bugs.Where(b => b.Project.OrganizationId == demo.Id).ExecuteDeleteAsync();
        await _db.Projects.Where(p => p.OrganizationId == demo.Id).ExecuteDeleteAsync();
        await _db.Organizations.Where(o => o.Id == demo.Id).ExecuteDeleteAsync();
        _db.ChangeTracker.Clear();
        var users = await _db.Users.CountAsync();

        await _seeder.SeedAsync();

        Assert.Equal(users, await _db.Users.CountAsync());
        Assert.Equal(5, await _db.Memberships.CountAsync(m => m.Organization.IsDemo));
        Assert.Equal(14, await _db.Bugs.CountAsync());
    }

    [Fact]
    public async Task TheDemo_IsMarkedSoItCannotBeChanged()
    {
        await _seeder.SeedAsync();

        Assert.True((await TheDemo()).IsDemo);
    }
}

// With the demo turned off (the default), seeding does nothing at all
public class DemoSeederDisabledTests
{
    [Fact]
    public async Task WhenTheDemoIsOff_NothingIsCreated()
    {
        using var app = new TestApp(demo: false);

        await app.Get<DemoSeeder>().SeedAsync();

        var db = app.Get<AppDbContext>();
        Assert.Empty(await db.Organizations.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }
}
