using System.Net;
using System.Net.Http.Json;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// A bug belongs to its project, and so to the project's organization. Everything said about
// projects in ProjectIsolationTests has to hold for their bugs too.
public class BugIsolationTests : IsolationTestBase
{
    private static async Task<PagedResultDto<BugDto>> Page(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<PagedResultDto<BugDto>>(Json))!;

    // ---- Inside the organization ----

    [Fact]
    public async Task APerson_CanReportAndReadBugsInTheirOwnProject()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);

        var bug = await CreateBug(Ana, project.Id);

        Assert.Equal(project.Id, (await GetBug(Ana, bug.Id)).ProjectId);
        var list = await Page(await Send(Ana, HttpMethod.Get, $"/api/projects/{project.Id}/bugs"));
        Assert.Equal(bug.Id, Assert.Single(list.Items).Id);
    }

    [Fact]
    public async Task AMember_CanReportEditAndDeleteBugs_InTheOrganizationTheyJoined()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await AddAsMember(Ben, Ana.OrganizationId);

        var bug = await CreateBug(Ben, project.Id, "Found by Ben");
        var patch = await Send(Ben, HttpMethod.Patch, $"/api/bugs/{bug.Id}", new { status = "Resolved" });
        var delete = await Send(Ben, HttpMethod.Delete, $"/api/bugs/{bug.Id}");

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    // ---- Outsiders see nothing ----

    [Fact]
    public async Task AnOutsider_CannotReportABugInAnotherOrganizationsProject()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);

        var response = await Send(Ben, HttpMethod.Post, $"/api/projects/{project.Id}/bugs",
            new { title = "Sneaky bug", description = "Should never be saved.", severity = "Low" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var list = await Page(await Send(Ana, HttpMethod.Get, $"/api/projects/{project.Id}/bugs"));
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task AnOutsider_CannotListAnotherOrganizationsBugsThroughTheProject()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await CreateBug(Ana, project.Id);

        var response = await Send(Ben, HttpMethod.Get, $"/api/projects/{project.Id}/bugs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task AnOutsider_CannotReadChangeOrDeleteAnotherOrganizationsBug(string method)
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        var bug = await CreateBug(Ana, project.Id);

        var response = await Send(Ben, new HttpMethod(method), $"/api/bugs/{bug.Id}", new
        {
            projectId = project.Id,
            title = "Hijacked",
            description = "Changed by an outsider.",
            severity = "Low",
            status = "Closed"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And the bug is exactly as it was
        var still = await GetBug(Ana, bug.Id);
        Assert.Equal("Neon sign flickers", still.Title);
        Assert.Equal(BugStatus.Open, still.Status);
    }

    [Fact]
    public async Task AnOutsider_CannotEmptyAnotherOrganizationsProject()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        var bug = await CreateBug(Ana, project.Id);

        var response = await Send(Ben, HttpMethod.Delete, $"/api/projects/{project.Id}/bugs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(bug.Id, (await GetBug(Ana, bug.Id)).Id); // still there
    }

    // ---- The search across projects ----

    [Fact]
    public async Task TheSearchAcrossProjects_OnlyReturnsBugsOfTheOrganizationsThePersonIsIn()
    {
        await SignUpAnaAndBen();
        var anas = await CreateProject(Ana, Ana.OrganizationId, "Ana's game", "ANA");
        var bens = await CreateProject(Ben, Ben.OrganizationId, "Ben's game", "BEN");
        await CreateBug(Ana, anas.Id, "Ana's bug");
        var bensBug = await CreateBug(Ben, bens.Id, "Ben's bug");

        var seenByBen = await Page(await Send(Ben, HttpMethod.Get, "/api/bugs"));

        Assert.Equal(bensBug.Id, Assert.Single(seenByBen.Items).Id);
        Assert.Equal(1, seenByBen.TotalCount);
    }

    [Fact]
    public async Task FilteringTheSearchByAnotherOrganizationsProject_FindsNothing()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await CreateBug(Ana, project.Id);

        var response = await Send(Ben, HttpMethod.Get, $"/api/bugs?projectId={project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await Page(response)).Items);
    }

    [Fact]
    public async Task ASearchByText_NeverReachesIntoAnotherOrganization()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await CreateBug(Ana, project.Id, "Unique-word-xyzzy");

        var response = await Send(Ben, HttpMethod.Get, "/api/bugs?search=xyzzy");

        Assert.Empty((await Page(response)).Items);
    }

    [Fact]
    public async Task APersonInTwoOrganizations_SearchesBothAtOnce()
    {
        await SignUpAnaAndBen();
        var anas = await CreateProject(Ana, Ana.OrganizationId, "Ana's game", "ANA");
        var bens = await CreateProject(Ben, Ben.OrganizationId, "Ben's game", "BEN");
        await CreateBug(Ana, anas.Id, "Ana's bug");
        await CreateBug(Ben, bens.Id, "Ben's bug");
        await AddAsMember(Ben, Ana.OrganizationId);

        var seenByBen = await Page(await Send(Ben, HttpMethod.Get, "/api/bugs"));

        Assert.Equal(2, seenByBen.TotalCount);
    }

    // ---- Moving a bug to another project ----

    [Fact]
    public async Task ABug_CanMoveToAnotherProjectOfTheSameOrganization()
    {
        await SignUpAnaAndBen();
        var first = await CreateProject(Ana, Ana.OrganizationId, "First", "AAA");
        var second = await CreateProject(Ana, Ana.OrganizationId, "Second", "BBB");
        var bug = await CreateBug(Ana, first.Id);

        var response = await Send(Ana, HttpMethod.Patch, $"/api/bugs/{bug.Id}", new { projectId = second.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(second.Id, (await GetBug(Ana, bug.Id)).ProjectId);
    }

    [Theory]
    [InlineData("PATCH")]
    [InlineData("PUT")]
    public async Task ABug_CannotMoveToAProjectOfAnotherOrganization_EvenIfThePersonCanSeeBoth(string method)
    {
        await SignUpAnaAndBen();
        var anas = await CreateProject(Ana, Ana.OrganizationId, "Ana's game", "ANA");
        var bens = await CreateProject(Ben, Ben.OrganizationId, "Ben's game", "BEN");
        var bug = await CreateBug(Ben, bens.Id);
        // Ben is now in both organizations, so he can see both projects
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Send(Ben, new HttpMethod(method), $"/api/bugs/{bug.Id}", new
        {
            projectId = anas.Id,
            title = "Moved",
            description = "Moving it out of its organization.",
            severity = "High",
            status = "Open"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(bens.Id, (await GetBug(Ben, bug.Id)).ProjectId); // stayed where it was
    }

    [Fact]
    public async Task ABug_CannotMoveToAProjectOfAnOrganizationThePersonIsNotIn()
    {
        await SignUpAnaAndBen();
        var anas = await CreateProject(Ana, Ana.OrganizationId, "Ana's game", "ANA");
        var bens = await CreateProject(Ben, Ben.OrganizationId, "Ben's game", "BEN");
        var bug = await CreateBug(Ben, bens.Id);

        var response = await Send(Ben, HttpMethod.Patch, $"/api/bugs/{bug.Id}", new { projectId = anas.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(bens.Id, (await GetBug(Ben, bug.Id)).ProjectId);
    }

    // ---- The counts on the project ----

    [Fact]
    public async Task AProjectsBugCount_OnlyCountsItsOwnBugs()
    {
        await SignUpAnaAndBen();
        var anas = await CreateProject(Ana, Ana.OrganizationId, "Ana's game", "ANA");
        var bens = await CreateProject(Ben, Ben.OrganizationId, "Ben's game", "BEN");
        await CreateBug(Ana, anas.Id);
        await CreateBug(Ana, anas.Id, "Second bug");
        await CreateBug(Ben, bens.Id);

        var project = (await (await Send(Ben, HttpMethod.Get, $"/api/projects/{bens.Id}"))
            .Content.ReadFromJsonAsync<ProjectDto>())!;

        Assert.Equal(1, project.BugCount);
    }
}
