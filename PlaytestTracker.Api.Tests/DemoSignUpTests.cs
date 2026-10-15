using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// Everybody who signs up also becomes a Member of the demo organization
public class DemoSignUpTests : IsolationTestBase
{
    public DemoSignUpTests() : base(demo: true) { }

    private async Task<List<OrganizationDto>> Mine(Person person)
    {
        var response = await Send(person, HttpMethod.Get, "/api/organizations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<OrganizationDto>>(Json))!;
    }

    private async Task<int> DemoId()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Organizations.Where(o => o.IsDemo).Select(o => o.Id).SingleAsync();
    }

    [Fact]
    public async Task ANewAccount_StartsWithItsOwnOrganizationAndTheDemo_TheDemoLast()
    {
        await SeedTheDemo();

        var visitor = await SignUp("visitor");

        var mine = await Mine(visitor);
        Assert.Equal(2, mine.Count);
        Assert.False(mine[0].IsDemo);
        Assert.Equal(OrganizationRole.Owner, mine[0].MyRole);
        Assert.Equal(visitor.OrganizationId, mine[0].Id);
        Assert.True(mine[1].IsDemo);
        Assert.Equal(OrganizationRole.Member, mine[1].MyRole);
        Assert.Equal(6, mine[1].MemberCount); // the five of the demo, and the new one
    }

    [Fact]
    public async Task ANewAccount_SeesTheDemoProjectsAndBugsRightAway()
    {
        await SeedTheDemo();
        var visitor = await SignUp("visitor");
        var demoId = await DemoId();

        var projects = await Send(visitor, HttpMethod.Get, $"/api/organizations/{demoId}/projects");
        var list = (await projects.Content.ReadFromJsonAsync<List<ProjectDto>>(Json))!;
        var pink = list.Single(p => p.Code == "PIN");
        var bugs = await Send(visitor, HttpMethod.Get, $"/api/projects/{pink.Id}/bugs");

        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);
        Assert.Equal(2, list.Count);
        Assert.Equal(HttpStatusCode.OK, bugs.StatusCode);
    }

    [Fact]
    public async Task ANewAccount_CanTryTheDemo_ButCannotLeaveIt()
    {
        await SeedTheDemo();
        var visitor = await SignUp("visitor");
        var demoId = await DemoId();
        var pink = (await (await Send(visitor, HttpMethod.Get, $"/api/organizations/{demoId}/projects"))
            .Content.ReadFromJsonAsync<List<ProjectDto>>(Json))!.Single(p => p.Code == "PIN");

        var bug = await CreateBug(visitor, pink.Id, "Reported by a visitor");
        var leave = await Send(visitor, HttpMethod.Delete, $"/api/organizations/{demoId}/members/{visitor.Id}");

        Assert.Equal(pink.Id, bug.ProjectId);
        Assert.Equal(HttpStatusCode.Forbidden, leave.StatusCode);
        Assert.Equal(2, (await Mine(visitor)).Count);
    }

    [Fact]
    public async Task EveryNewAccount_JoinsTheDemo_AndAppearsInItsMemberList()
    {
        await SeedTheDemo();
        var first = await SignUp("first");
        var second = await SignUp("second");
        var third = await SignUp("third");
        var demoId = await DemoId();

        var response = await Send(first, HttpMethod.Get, $"/api/organizations/{demoId}/members");
        var members = (await response.Content.ReadFromJsonAsync<PagedResultDto<MemberDto>>(Json))!.Items;

        Assert.Equal(8, members.Count);
        Assert.Equal(1, members.Count(m => m.Role == OrganizationRole.Owner)); // still just Luna
        Assert.Contains(members, m => m.UserId == second.Id);
        Assert.Contains(members, m => m.UserId == third.Id);
    }

    [Fact]
    public async Task TwoVisitors_ShareTheDemo_ButNotTheirPersonalOrganizations()
    {
        await SeedTheDemo();
        var first = await SignUp("first");
        var second = await SignUp("second");
        var project = await CreateProject(first, first.OrganizationId);

        var theirs = await Send(second, HttpMethod.Get, $"/api/projects/{project.Id}");
        var theirOrganization = await Send(second, HttpMethod.Get, $"/api/organizations/{first.OrganizationId}");
        var members = await Send(second, HttpMethod.Get, $"/api/organizations/{first.OrganizationId}/members");

        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirOrganization.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, members.StatusCode);
    }

    [Fact]
    public async Task TheDemoOwner_StillSeesHerPersonalOrganizationFirst()
    {
        await SeedTheDemo();
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "luna@demo.test", password = TestApp.DemoPassword });
        var body = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;

        var mine = await Mine(new Person(body.Token, body.User.Id, 0));

        Assert.False(mine[0].IsDemo);
        Assert.True(mine[^1].IsDemo);
    }

    [Fact]
    public async Task WhenTheDemoIsOnButNotCreatedYet_SigningUpWorksAsAlways()
    {
        // No SeedTheDemo(): there is no demo organization to join
        var visitor = await SignUp("visitor");

        var mine = await Mine(visitor);

        var personal = Assert.Single(mine);
        Assert.Equal(visitor.OrganizationId, personal.Id);
    }

    [Fact]
    public async Task ASignUpThatFails_DoesNotJoinAnybodyToTheDemo()
    {
        await SeedTheDemo();
        var before = await MembershipsOfTheDemo();

        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "weak@test.local", password = "short", displayName = "Weak" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await MembershipsOfTheDemo());
    }

    private async Task<int> MembershipsOfTheDemo()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Memberships.CountAsync(m => m.Organization.IsDemo);
    }
}

// With the demo turned off, nobody is added to a demo, even if a demo organization is still in the database
public class DemoOffSignUpTests : IsolationTestBase
{
    [Fact]
    public async Task WhenTheDemoIsOff_NewAccountsDoNotJoinIt()
    {
        var leftover = await SignUp("owner");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Organizations.Where(o => o.Id == leftover.OrganizationId)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.IsDemo, true));
        }

        var visitor = await SignUp("visitor");

        var response = await Send(visitor, HttpMethod.Get, "/api/organizations");
        var mine = (await response.Content.ReadFromJsonAsync<List<OrganizationDto>>(Json))!;
        Assert.Equal(visitor.OrganizationId, Assert.Single(mine).Id);
    }
}
