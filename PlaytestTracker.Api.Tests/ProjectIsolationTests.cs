using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// The point of organizations: what belongs to one is invisible to everybody outside it.
// Ana and Ben each have their own organization; Cleo is added to Ana's as a Member when a test needs it.
public class ProjectIsolationTests : IDisposable
{
    private readonly ApiFactory _factory = new ApiFactory().WithEmptyDatabase();
    private readonly HttpClient _client;

    private Person _ana = null!;
    private Person _ben = null!;

    public ProjectIsolationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // A person who signed up: their token, and the id of the personal organization they got
    private sealed record Person(string Token, string Id, int OrganizationId);

    private async Task<Person> SignUp(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{name}@test.local", password = "Sunflower7", displayName = name });
        var body = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var organizationId = await db.Memberships
            .Where(m => m.UserId == body.User.Id)
            .Select(m => m.OrganizationId)
            .SingleAsync();

        return new Person(body.Token, body.User.Id, organizationId);
    }

    // Makes a person a Member of someone else's organization (there is no endpoint for this yet)
    private async Task AddAsMember(Person person, int organizationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Memberships.Add(new Membership
        {
            OrganizationId = organizationId,
            UserId = person.Id,
            Role = OrganizationRole.Member,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> Send(Person person, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", person.Token);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    private async Task<ProjectDto> CreateProject(Person person, int organizationId, string name = "Pink in the Night", string code = "PIN")
    {
        var response = await Send(person, HttpMethod.Post, $"/api/organizations/{organizationId}/projects",
            new { name, code });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private async Task SignUpAnaAndBen()
    {
        _ana = await SignUp("ana");
        _ben = await SignUp("ben");
    }

    // ---- What a person can do in their own organization ----

    [Fact]
    public async Task APerson_CanCreateAndListProjectsInTheirOwnOrganization()
    {
        await SignUpAnaAndBen();

        var created = await CreateProject(_ana, _ana.OrganizationId);

        Assert.Equal(_ana.OrganizationId, created.OrganizationId);

        var list = await Send(_ana, HttpMethod.Get, $"/api/organizations/{_ana.OrganizationId}/projects");
        var projects = (await list.Content.ReadFromJsonAsync<List<ProjectDto>>())!;
        Assert.Equal(created.Id, Assert.Single(projects).Id);
    }

    [Fact]
    public async Task ACreatedProject_ReportsWhereToFindItInTheLocationHeader()
    {
        await SignUpAnaAndBen();

        var response = await Send(_ana, HttpMethod.Post, $"/api/organizations/{_ana.OrganizationId}/projects",
            new { name = "Pink in the Night", code = "PIN" });

        var created = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.EndsWith($"/api/Projects/{created.Id}", response.Headers.Location!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Outsiders see nothing ----

    [Fact]
    public async Task AnOutsider_CannotListAnotherOrganizationsProjects()
    {
        await SignUpAnaAndBen();
        await CreateProject(_ana, _ana.OrganizationId);

        var response = await Send(_ben, HttpMethod.Get, $"/api/organizations/{_ana.OrganizationId}/projects");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnOutsider_CannotCreateAProjectInAnotherOrganization()
    {
        await SignUpAnaAndBen();

        var response = await Send(_ben, HttpMethod.Post, $"/api/organizations/{_ana.OrganizationId}/projects",
            new { name = "Sneaky", code = "SNK" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var list = await Send(_ana, HttpMethod.Get, $"/api/organizations/{_ana.OrganizationId}/projects");
        Assert.Empty((await list.Content.ReadFromJsonAsync<List<ProjectDto>>())!);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AnOutsider_CannotReadChangeOrDeleteAnotherOrganizationsProject(string method)
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(_ana, _ana.OrganizationId);

        var response = await Send(_ben, new HttpMethod(method), $"/api/projects/{project.Id}",
            new { name = "Hijacked", code = "HIJ" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And it is exactly as it was
        var still = await Send(_ana, HttpMethod.Get, $"/api/projects/{project.Id}");
        var body = (await still.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal("Pink in the Night", body.Name);
        Assert.Equal("PIN", body.Code);
    }

    [Fact]
    public async Task ARealOrganizationAndOneThatDoesNotExist_AreAnsweredTheSame()
    {
        await SignUpAnaAndBen();

        var someoneElses = await Send(_ben, HttpMethod.Get, $"/api/organizations/{_ana.OrganizationId}/projects");
        var missing = await Send(_ben, HttpMethod.Get, "/api/organizations/9999/projects");

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

        await CreateProject(_ana, _ana.OrganizationId, code: "PIN");
        var bens = await CreateProject(_ben, _ben.OrganizationId, code: "PIN");

        Assert.Equal("PIN", bens.Code);
    }

    [Fact]
    public async Task InsideOneOrganization_ACodeCannotBeUsedTwice()
    {
        await SignUpAnaAndBen();
        await CreateProject(_ana, _ana.OrganizationId, code: "PIN");

        var response = await Send(_ana, HttpMethod.Post, $"/api/organizations/{_ana.OrganizationId}/projects",
            new { name = "Another game", code = "pin" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangingAProjectsCode_ToOneUsedOnlyInAnotherOrganization_IsFine()
    {
        await SignUpAnaAndBen();
        await CreateProject(_ben, _ben.OrganizationId, code: "XYZ");
        var project = await CreateProject(_ana, _ana.OrganizationId, code: "PIN");

        var response = await Send(_ana, HttpMethod.Put, $"/api/projects/{project.Id}",
            new { name = "Pink in the Night", code = "XYZ" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Members and the Owner ----

    [Fact]
    public async Task AMember_SeesCreatesAndEditsProjects_OfTheOrganizationTheyJoined()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(_ana, _ana.OrganizationId);
        await AddAsMember(_ben, _ana.OrganizationId);

        var list = await Send(_ben, HttpMethod.Get, $"/api/organizations/{_ana.OrganizationId}/projects");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Single((await list.Content.ReadFromJsonAsync<List<ProjectDto>>())!);

        var edit = await Send(_ben, HttpMethod.Put, $"/api/projects/{project.Id}",
            new { name = "Renamed by Ben", code = "PIN" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var create = await Send(_ben, HttpMethod.Post, $"/api/organizations/{_ana.OrganizationId}/projects",
            new { name = "Ben's idea", code = "BEN" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task AMember_CannotDeleteAProject_ButTheOwnerCan()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(_ana, _ana.OrganizationId);
        await AddAsMember(_ben, _ana.OrganizationId);

        var byMember = await Send(_ben, HttpMethod.Delete, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);

        var byOwner = await Send(_ana, HttpMethod.Delete, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);
    }

    [Fact]
    public async Task APersonInTwoOrganizations_SeesEachOnesProjectsSeparately()
    {
        await SignUpAnaAndBen();
        await CreateProject(_ana, _ana.OrganizationId, "Ana's game", "ANA");
        await CreateProject(_ben, _ben.OrganizationId, "Ben's game", "BEN");
        await AddAsMember(_ben, _ana.OrganizationId);

        var inAnas = await Send(_ben, HttpMethod.Get, $"/api/organizations/{_ana.OrganizationId}/projects");
        var inBens = await Send(_ben, HttpMethod.Get, $"/api/organizations/{_ben.OrganizationId}/projects");

        Assert.Equal("ANA", Assert.Single((await inAnas.Content.ReadFromJsonAsync<List<ProjectDto>>())!).Code);
        Assert.Equal("BEN", Assert.Single((await inBens.Content.ReadFromJsonAsync<List<ProjectDto>>())!).Code);
    }
}
