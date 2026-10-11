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

// The demo organization as the people in it use it, through the API
public class DemoEndpointsTests : IsolationTestBase
{
    private Person _luna = null!; // the Owner
    private Person _theo = null!; // a Member
    private int _demoId;

    public DemoEndpointsTests() : base(demo: true) { }

    private async Task StartWithTheDemo()
    {
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();

        _luna = await LogIn("luna@demo.test");
        _theo = await LogIn("theo@demo.test");

        using var check = Factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        _demoId = await db.Organizations.Where(o => o.IsDemo).Select(o => o.Id).SingleAsync();
    }

    // Signs in the way a visitor would: with the public demo account
    private async Task<Person> LogIn(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email, password = TestApp.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        return new Person(body.Token, body.User.Id, OrganizationId: 0);
    }

    private async Task<List<ProjectDto>> DemoProjects(Person person)
    {
        var response = await Send(person, HttpMethod.Get, $"/api/organizations/{_demoId}/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ProjectDto>>(Json))!;
    }

    // ---- What a visitor finds ----

    [Fact]
    public async Task TheDemoOwner_SeesTheDemoAmongTheirOrganizations_WithEverythingInIt()
    {
        await StartWithTheDemo();

        var response = await Send(_luna, HttpMethod.Get, "/api/organizations");
        var mine = (await response.Content.ReadFromJsonAsync<List<OrganizationDto>>(Json))!;

        var demo = Assert.Single(mine, o => o.IsDemo);
        Assert.Equal("Pink in the Night Studio", demo.Name);
        Assert.Equal(OrganizationRole.Owner, demo.MyRole);
        Assert.Equal(5, demo.MemberCount);
        Assert.Equal(2, demo.ProjectCount);
        Assert.Equal(2, mine.Count); // the demo and her personal one
    }

    [Fact]
    public async Task ADemoMember_SeesTheProjects_WithTheirBugCounts()
    {
        await StartWithTheDemo();

        var projects = await DemoProjects(_theo);

        Assert.Equal(2, projects.Count);
        var pink = Assert.Single(projects, p => p.Code == "PIN");
        Assert.Equal(9, pink.BugCount);
        Assert.True(pink.OpenCriticalCount > 0);
        Assert.Equal(5, Assert.Single(projects, p => p.Code == "NEO").BugCount);
    }

    [Fact]
    public async Task TheDemoPeople_AreListedWithTheOwnerFirst()
    {
        await StartWithTheDemo();

        var response = await Send(_theo, HttpMethod.Get, $"/api/organizations/{_demoId}/members");
        var members = (await response.Content.ReadFromJsonAsync<List<MemberDto>>(Json))!;

        Assert.Equal(5, members.Count);
        Assert.Equal("Luna Park", members[0].DisplayName);
        Assert.Equal(OrganizationRole.Owner, members[0].Role);
    }

    // ---- The data can be played with ----

    [Fact]
    public async Task ADemoMember_CanReportEditAndDeleteBugs()
    {
        await StartWithTheDemo();
        var pink = (await DemoProjects(_theo)).Single(p => p.Code == "PIN");

        var bug = await CreateBug(_theo, pink.Id, "A bug found by a visitor");
        var patch = await Send(_theo, HttpMethod.Patch, $"/api/bugs/{bug.Id}", new { status = "Resolved" });
        var delete = await Send(_theo, HttpMethod.Delete, $"/api/bugs/{bug.Id}");

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task ADemoMember_CanCreateAndEditProjects()
    {
        await StartWithTheDemo();

        var project = await CreateProject(_theo, _demoId, "My own game", "OWN");
        var edit = await Send(_theo, HttpMethod.Put, $"/api/projects/{project.Id}",
            new { name = "My own game, renamed", code = "OWN" });

        Assert.Equal(_demoId, project.OrganizationId);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(3, (await DemoProjects(_theo)).Count);
    }

    // ---- But its structure is fixed ----

    [Fact]
    public async Task TheDemoOrganization_CannotBeRenamedOrDeleted_NotEvenByItsOwner()
    {
        await StartWithTheDemo();

        var rename = await Send(_luna, HttpMethod.Put, $"/api/organizations/{_demoId}", new { name = "Mine now" });
        var delete = await Send(_luna, HttpMethod.Delete, $"/api/organizations/{_demoId}");

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task TheDemosPeople_CannotChange_NotEvenByItsOwner()
    {
        await StartWithTheDemo();
        var newcomer = await SignUp("visitor");

        var add = await Send(_luna, HttpMethod.Post, $"/api/organizations/{_demoId}/members",
            new { email = "visitor@test.local" });
        var remove = await Send(_luna, HttpMethod.Delete, $"/api/organizations/{_demoId}/members/{_theo.Id}");
        var leave = await Send(_theo, HttpMethod.Delete, $"/api/organizations/{_demoId}/members/{_theo.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leave.StatusCode);
        Assert.NotNull(newcomer);
    }

    [Fact]
    public async Task TheDemoOwner_CanStillDeleteDemoProjects_ThatHaveNoBugs()
    {
        await StartWithTheDemo();
        var project = await CreateProject(_luna, _demoId, "Scratch project", "SCR");

        var delete = await Send(_luna, HttpMethod.Delete, $"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
