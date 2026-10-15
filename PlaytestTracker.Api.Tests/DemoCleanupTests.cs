using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// The weekly cleanup of the demo: who may ask for it, what it puts back, and who it deletes
public class DemoCleanupTests : IsolationTestBase
{
    private const string CleanPath = "/api/maintenance/clean-demo";

    public DemoCleanupTests() : base(demo: true) { }

    private Task<HttpResponseMessage> Clean(string? key = TestApp.MaintenanceKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CleanPath);
        if (key != null)
            request.Headers.Add("X-Maintenance-Key", key);
        return Client.SendAsync(request);
    }

    private async Task<CleanupResult> CleanOk()
    {
        var response = await Clean();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CleanupResult>(Json))!;
    }

    // Makes somebody's sign-up look `days` days old (the date they joined the demo is the date they signed up)
    private async Task MakeOld(Person person, int days)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.Memberships.SingleAsync(m => m.UserId == person.Id && m.Organization.IsDemo);
        membership.JoinedAt = DateTimeOffset.UtcNow.AddDays(-days);
        await db.SaveChangesAsync();
    }

    private async Task<T> InDatabase<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<bool> CanSignIn(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        return response.StatusCode == HttpStatusCode.OK;
    }

    private Task<bool> UserExists(Person person) => InDatabase(db => db.Users.AnyAsync(u => u.Id == person.Id));

    // ---- Who may ask ----

    [Fact]
    public async Task WithoutTheKey_IsRefused()
    {
        await SeedTheDemo();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Clean(key: null)).StatusCode);
    }

    [Fact]
    public async Task WithAWrongKey_IsRefused_AndNothingIsDeleted()
    {
        await SeedTheDemo();
        var old = await SignUp("old");
        await MakeOld(old, days: 30);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Clean(key: "not-the-key")).StatusCode);

        Assert.True(await UserExists(old));
    }

    [Fact]
    public async Task ASignedInPerson_CannotUseItWithTheirToken()
    {
        await SeedTheDemo();
        var ana = await SignUp("ana");

        var response = await Send(ana, HttpMethod.Post, CleanPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]                       // not configured (the default everywhere)
    [InlineData("too-short-to-be-safe")]   // configured, but weak: ignored
    public async Task WithoutAUsableKeyConfigured_TheEndpointDoesNotExist(string configuredKey)
    {
        using var factory = new ApiFactory(demo: true, maintenanceKey: configuredKey).WithEmptyDatabase();
        using var client = factory.CreateClient();

        // Not even the "right" key opens it
        var request = new HttpRequestMessage(HttpMethod.Post, CleanPath);
        request.Headers.Add("X-Maintenance-Key", configuredKey.Length > 0 ? configuredKey : TestApp.MaintenanceKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- What it puts back ----

    [Fact]
    public async Task TheDemoContent_GoesBackToHowItWasFirstMade()
    {
        await SeedTheDemo();
        var luna = await InDatabase(async db => await db.Users.SingleAsync(u => u.Email == "luna@demo.test"));
        var demoId = await InDatabase(db => db.Organizations.Where(o => o.IsDemo).Select(o => o.Id).SingleAsync());
        var token = await LogInAs("luna@demo.test");
        var lunaPerson = new Person(token, luna.Id, demoId);

        // A visitor vandalizes the demo: deletes a project and its bugs, and adds a project of their own
        var pink = await InDatabase(db => db.Projects.SingleAsync(p => p.Code == "PIN"));
        Assert.Equal(HttpStatusCode.NoContent, (await Send(lunaPerson, HttpMethod.Delete, $"/api/projects/{pink.Id}/bugs")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(lunaPerson, HttpMethod.Delete, $"/api/projects/{pink.Id}")).StatusCode);
        await CreateProject(lunaPerson, demoId, "Graffiti", "XXX");

        var result = await CleanOk();

        Assert.True(result.DemoRestored);
        var projects = await InDatabase(db => db.Projects.Where(p => p.OrganizationId == demoId).ToListAsync());
        Assert.Equal(["NEO", "PIN"], projects.Select(p => p.Code).Order());
        Assert.Equal(14, await InDatabase(db => db.Bugs.CountAsync(b => b.Project.OrganizationId == demoId)));
    }

    [Fact]
    public async Task TheDemoPeople_AreNeverDeleted_EvenThoughTheyJoinedLongAgo()
    {
        await SeedTheDemo();

        var result = await CleanOk();

        Assert.Equal(0, result.VisitorsDeleted);
        foreach (var (email, _) in DemoSeeder.People)
            Assert.True(await CanSignIn(email, TestApp.DemoPassword));
        Assert.Equal(5, await InDatabase(db => db.Memberships.CountAsync(m => m.Organization.IsDemo)));
    }

    // ---- Who it deletes ----

    [Fact]
    public async Task AVisitorWhoSignedUpLongAgo_IsDeleted_WithTheirOrganizationAndEverythingInIt()
    {
        await SeedTheDemo();
        var old = await SignUp("old");
        var project = await CreateProject(old, old.OrganizationId);
        await CreateBug(old, project.Id);
        await MakeOld(old, days: 8);

        var result = await CleanOk();

        Assert.Equal(1, result.VisitorsDeleted);
        Assert.False(await UserExists(old));
        Assert.False(await CanSignIn("old@test.local", "Sunflower7"));
        Assert.False(await InDatabase(db => db.Organizations.AnyAsync(o => o.Id == old.OrganizationId)));
        Assert.False(await InDatabase(db => db.Projects.AnyAsync(p => p.OrganizationId == old.OrganizationId)));
        Assert.False(await InDatabase(db => db.Bugs.AnyAsync(b => b.ProjectId == project.Id)));
        Assert.False(await InDatabase(db => db.Memberships.AnyAsync(m => m.UserId == old.Id)));
    }

    [Fact]
    public async Task ARecentVisitor_StaysWithEverything()
    {
        await SeedTheDemo();
        var recent = await SignUp("recent");
        var project = await CreateProject(recent, recent.OrganizationId);
        await MakeOld(recent, days: 6);

        var result = await CleanOk();

        Assert.Equal(0, result.VisitorsDeleted);
        Assert.True(await UserExists(recent));
        Assert.True(await CanSignIn("recent@test.local", "Sunflower7"));
        Assert.True(await InDatabase(db => db.Projects.AnyAsync(p => p.Id == project.Id)));
    }

    [Fact]
    public async Task OnlyTheOldOnesGo_WhenThereAreBoth()
    {
        await SeedTheDemo();
        var old = await SignUp("old");
        var recent = await SignUp("recent");
        await MakeOld(old, days: 20);

        var result = await CleanOk();

        Assert.Equal(1, result.VisitorsDeleted);
        Assert.False(await UserExists(old));
        Assert.True(await UserExists(recent));
    }

    [Fact]
    public async Task TwoOldVisitors_WhoShareAnOrganization_AreBothDeleted_WithIt()
    {
        await SeedTheDemo();
        var ana = await SignUp("ana");
        var ben = await SignUp("ben");
        await AddAsMember(ben, ana.OrganizationId);
        await CreateProject(ana, ana.OrganizationId);
        await MakeOld(ana, days: 10);
        await MakeOld(ben, days: 10);

        var result = await CleanOk();

        Assert.Equal(2, result.VisitorsDeleted);
        Assert.Equal(0, result.VisitorsKept);
        Assert.False(await InDatabase(db => db.Organizations.AnyAsync(o => o.Id == ana.OrganizationId)));
    }

    [Fact]
    public async Task AnOldVisitor_WhoOwnsAnOrganizationARecentVisitorUses_StaysForNow()
    {
        await SeedTheDemo();
        var ana = await SignUp("ana");   // old, owns an organization
        var ben = await SignUp("ben");   // recent, was added to it
        await AddAsMember(ben, ana.OrganizationId);
        var project = await CreateProject(ana, ana.OrganizationId);
        await MakeOld(ana, days: 10);

        var result = await CleanOk();

        // Deleting Ana's organization would take Ben's work away, so Ana stays until Ben is old too
        Assert.Equal(0, result.VisitorsDeleted);
        Assert.Equal(1, result.VisitorsKept);
        Assert.True(await UserExists(ana));
        Assert.True(await InDatabase(db => db.Projects.AnyAsync(p => p.Id == project.Id)));
        Assert.Equal(HttpStatusCode.OK, (await Send(ben, HttpMethod.Get, $"/api/projects/{project.Id}")).StatusCode);
    }

    [Fact]
    public async Task AnOldVisitor_WhoIsOnlyAMemberOfAnotherOrganization_IsDeleted_AndThatOrganizationStays()
    {
        await SeedTheDemo();
        var ana = await SignUp("ana");   // recent, owns an organization
        var ben = await SignUp("ben");   // old, only a member of Ana's
        await AddAsMember(ben, ana.OrganizationId);
        var project = await CreateProject(ana, ana.OrganizationId);
        await MakeOld(ben, days: 10);

        var result = await CleanOk();

        Assert.Equal(1, result.VisitorsDeleted);
        Assert.False(await UserExists(ben));
        Assert.True(await UserExists(ana));
        Assert.True(await InDatabase(db => db.Projects.AnyAsync(p => p.Id == project.Id)));
        Assert.Equal(1, await InDatabase(db => db.Memberships.CountAsync(m => m.OrganizationId == ana.OrganizationId)));
    }

    [Fact]
    public async Task RunningItTwice_ChangesNothingTheSecondTime()
    {
        await SeedTheDemo();
        var old = await SignUp("old");
        await MakeOld(old, days: 10);

        await CleanOk();
        var second = await CleanOk();

        Assert.Equal(0, second.VisitorsDeleted);
        Assert.Equal(14, await InDatabase(db => db.Bugs.CountAsync()));
    }

    [Fact]
    public async Task WithTheDemoTurnedOff_ItDoesNothing()
    {
        using var factory = new ApiFactory(demo: false).WithEmptyDatabase();
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, CleanPath);
        request.Headers.Add("X-Maintenance-Key", TestApp.MaintenanceKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CleanupResult>(Json))!;
        Assert.False(result.DemoRestored);
        Assert.Equal(0, result.VisitorsDeleted);
    }

    private async Task<string> LogInAs(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password = TestApp.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token;
    }
}
