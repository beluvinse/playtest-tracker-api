using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// The point of organizations: what belongs to one is invisible to everybody outside it.
// Ana and Ben each have their own organization; Ben is added to Ana's as a Member when a test needs it.
public class ProjectIsolationTests : IsolationTestBase
{
    // ---- What a person can do in their own organization ----

    [Fact]
    public async Task APerson_CanCreateAndListProjectsInTheirOwnOrganization()
    {
        await SignUpAnaAndBen();

        var created = await CreateProject(Ana, Ana.OrganizationId);

        Assert.Equal(Ana.OrganizationId, created.OrganizationId);

        var list = await Send(Ana, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");
        var projects = (await list.Content.ReadFromJsonAsync<List<ProjectDto>>())!;
        Assert.Equal(created.Id, Assert.Single(projects).Id);
    }

    [Fact]
    public async Task ACreatedProject_ReportsWhereToFindItInTheLocationHeader()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Post, $"/api/organizations/{Ana.OrganizationId}/projects",
            new { name = "Pink in the Night", code = "PIN" });

        var created = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.EndsWith($"/api/Projects/{created.Id}", response.Headers.Location!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Outsiders see nothing ----

    [Fact]
    public async Task AnOutsider_CannotListAnotherOrganizationsProjects()
    {
        await SignUpAnaAndBen();
        await CreateProject(Ana, Ana.OrganizationId);

        var response = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnOutsider_CannotCreateAProjectInAnotherOrganization()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ben, HttpMethod.Post, $"/api/organizations/{Ana.OrganizationId}/projects",
            new { name = "Sneaky", code = "SNK" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var list = await Send(Ana, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");
        Assert.Empty((await list.Content.ReadFromJsonAsync<List<ProjectDto>>())!);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AnOutsider_CannotReadChangeOrDeleteAnotherOrganizationsProject(string method)
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);

        var response = await Send(Ben, new HttpMethod(method), $"/api/projects/{project.Id}",
            new { name = "Hijacked", code = "HIJ" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And it is exactly as it was
        var still = await Send(Ana, HttpMethod.Get, $"/api/projects/{project.Id}");
        var body = (await still.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal("Pink in the Night", body.Name);
        Assert.Equal("PIN", body.Code);
    }

    [Fact]
    public async Task ARealOrganizationAndOneThatDoesNotExist_AreAnsweredTheSame()
    {
        await SignUpAnaAndBen();

        var someoneElses = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");
        var missing = await Send(Ben, HttpMethod.Get, "/api/organizations/9999/projects");

        // Nothing in the answer lets an outsider find out which organizations exist
        Assert.Equal(missing.StatusCode, someoneElses.StatusCode);
        var missingBody = (await missing.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var someoneElsesBody = (await someoneElses.Content.ReadFromJsonAsync<ProblemDetails>())!;
        // (the traceId is different on every request, so it isn't compared)
        Assert.Equal(missingBody.Title, someoneElsesBody.Title);
        Assert.Equal(missingBody.Type, someoneElsesBody.Type);
        Assert.Equal(missingBody.Detail, someoneElsesBody.Detail);
    }

    // ---- Codes belong to an organization ----

    [Fact]
    public async Task TwoOrganizations_CanUseTheSameProjectCode()
    {
        await SignUpAnaAndBen();

        await CreateProject(Ana, Ana.OrganizationId, code: "PIN");
        var bens = await CreateProject(Ben, Ben.OrganizationId, code: "PIN");

        Assert.Equal("PIN", bens.Code);
    }

    [Fact]
    public async Task InsideOneOrganization_ACodeCannotBeUsedTwice()
    {
        await SignUpAnaAndBen();
        await CreateProject(Ana, Ana.OrganizationId, code: "PIN");

        var response = await Send(Ana, HttpMethod.Post, $"/api/organizations/{Ana.OrganizationId}/projects",
            new { name = "Another game", code = "pin" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangingAProjectsCode_ToOneUsedOnlyInAnotherOrganization_IsFine()
    {
        await SignUpAnaAndBen();
        await CreateProject(Ben, Ben.OrganizationId, code: "XYZ");
        var project = await CreateProject(Ana, Ana.OrganizationId, code: "PIN");

        var response = await Send(Ana, HttpMethod.Put, $"/api/projects/{project.Id}",
            new { name = "Pink in the Night", code = "XYZ" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Members and the Owner ----

    [Fact]
    public async Task AMember_SeesCreatesAndEditsProjects_OfTheOrganizationTheyJoined()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await AddAsMember(Ben, Ana.OrganizationId);

        var list = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Single((await list.Content.ReadFromJsonAsync<List<ProjectDto>>())!);

        var edit = await Send(Ben, HttpMethod.Put, $"/api/projects/{project.Id}",
            new { name = "Renamed by Ben", code = "PIN" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var create = await Send(Ben, HttpMethod.Post, $"/api/organizations/{Ana.OrganizationId}/projects",
            new { name = "Ben's idea", code = "BEN" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task AMember_CannotDeleteAProject_ButTheOwnerCan()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await AddAsMember(Ben, Ana.OrganizationId);

        var byMember = await Send(Ben, HttpMethod.Delete, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);

        var byOwner = await Send(Ana, HttpMethod.Delete, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);
    }

    [Fact]
    public async Task APersonInTwoOrganizations_SeesEachOnesProjectsSeparately()
    {
        await SignUpAnaAndBen();
        await CreateProject(Ana, Ana.OrganizationId, "Ana's game", "ANA");
        await CreateProject(Ben, Ben.OrganizationId, "Ben's game", "BEN");
        await AddAsMember(Ben, Ana.OrganizationId);

        var inAnas = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");
        var inBens = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ben.OrganizationId}/projects");

        Assert.Equal("ANA", Assert.Single((await inAnas.Content.ReadFromJsonAsync<List<ProjectDto>>())!).Code);
        Assert.Equal("BEN", Assert.Single((await inBens.Content.ReadFromJsonAsync<List<ProjectDto>>())!).Code);
    }
}
