using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// Listing, reading, creating and renaming organizations
public class OrganizationEndpointsTests : IsolationTestBase
{
    private async Task<List<OrganizationDto>> Mine(Person person)
    {
        var response = await Send(person, HttpMethod.Get, "/api/organizations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<OrganizationDto>>(Json))!;
    }

    private async Task<OrganizationDto> Create(Person person, string name)
    {
        var response = await Send(person, HttpMethod.Post, "/api/organizations", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrganizationDto>(Json))!;
    }

    // Marks an organization as the demo one (the endpoint to set that up comes later)
    private async Task MakeDemo(int organizationId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Organizations.Where(o => o.Id == organizationId)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.IsDemo, true));
    }

    // ---- GET /api/organizations ----

    [Fact]
    public async Task MyOrganizations_StartsWithThePersonalOneWhereIAmTheOwner()
    {
        await SignUpAnaAndBen();

        var mine = await Mine(Ana);

        var personal = Assert.Single(mine);
        Assert.Equal(Ana.OrganizationId, personal.Id);
        Assert.Equal("ana's space", personal.Name, ignoreCase: true);
        Assert.Equal(OrganizationRole.Owner, personal.MyRole);
        Assert.Equal(1, personal.MemberCount);
        Assert.Equal(0, personal.ProjectCount);
        Assert.False(personal.IsDemo);
    }

    [Fact]
    public async Task MyOrganizations_ShowsTheRoleIHaveInEach_AndTheirCounts()
    {
        await SignUpAnaAndBen();
        await CreateProject(Ana, Ana.OrganizationId);
        await AddAsMember(Ben, Ana.OrganizationId);

        var seenByBen = await Mine(Ben);

        Assert.Equal(2, seenByBen.Count);
        var anas = seenByBen.Single(o => o.Id == Ana.OrganizationId);
        Assert.Equal(OrganizationRole.Member, anas.MyRole);
        Assert.Equal(2, anas.MemberCount);
        Assert.Equal(1, anas.ProjectCount);
        var bens = seenByBen.Single(o => o.Id == Ben.OrganizationId);
        Assert.Equal(OrganizationRole.Owner, bens.MyRole);
        Assert.Equal(0, bens.ProjectCount); // Ana's project is not counted in Ben's own organization
    }

    [Fact]
    public async Task MyOrganizations_NeverIncludesOthersPeoplesOrganizations()
    {
        await SignUpAnaAndBen();

        var seenByBen = await Mine(Ben);

        Assert.DoesNotContain(seenByBen, o => o.Id == Ana.OrganizationId);
    }

    // ---- POST /api/organizations ----

    [Fact]
    public async Task ACreatedOrganization_HasMeAsItsOnlyMember_AndOwner()
    {
        await SignUpAnaAndBen();

        var created = await Create(Ana, "  Night Owls Studio  ");

        Assert.Equal("Night Owls Studio", created.Name); // spaces around the name are trimmed
        Assert.Equal(OrganizationRole.Owner, created.MyRole);
        Assert.Equal(1, created.MemberCount);
        Assert.False(created.IsDemo);
        Assert.Contains(await Mine(Ana), o => o.Id == created.Id);
        Assert.DoesNotContain(await Mine(Ben), o => o.Id == created.Id);
    }

    [Fact]
    public async Task ACreatedOrganization_ReportsWhereToFindItInTheLocationHeader()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Post, "/api/organizations", new { name = "Night Owls" });

        var created = (await response.Content.ReadFromJsonAsync<OrganizationDto>(Json))!;
        Assert.EndsWith($"/api/Organizations/{created.Id}", response.Headers.Location!.AbsolutePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ANewOrganization_StartsWithNoProjects_AndCanHaveProjectsRightAway()
    {
        await SignUpAnaAndBen();
        var created = await Create(Ana, "Night Owls");

        var project = await CreateProject(Ana, created.Id, "Pink in the Night", "PIN");

        Assert.Equal(created.Id, project.OrganizationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   ")]
    public async Task CreatingAnOrganization_WithAnInvalidName_Returns400(string name)
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Post, "/api/organizations", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await Mine(Ana)); // only her personal one
    }

    [Fact]
    public async Task CreatingAnOrganization_WithATooLongName_Returns400()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Post, "/api/organizations",
            new { name = new string('x', Organization.NameMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- GET /api/organizations/{id} ----

    [Fact]
    public async Task AMember_CanReadTheOrganization_ButAnOutsiderGets404()
    {
        await SignUpAnaAndBen();

        var asOwner = await Send(Ana, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}");
        var asOutsider = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}");
        var missing = await Send(Ben, HttpMethod.Get, "/api/organizations/9999");

        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        // Someone else's organization and one that doesn't exist look exactly the same
        var outsiderBody = (await asOutsider.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var missingBody = (await missing.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(missingBody.Title, outsiderBody.Title);
        Assert.Equal(missingBody.Detail, outsiderBody.Detail);
    }

    // ---- PUT /api/organizations/{id} (rename) ----

    [Fact]
    public async Task TheOwner_CanRenameTheOrganization()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Put, $"/api/organizations/{Ana.OrganizationId}",
            new { name = "Ana Games" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renamed = (await response.Content.ReadFromJsonAsync<OrganizationDto>(Json))!;
        Assert.Equal("Ana Games", renamed.Name);
        Assert.Equal("Ana Games", (await Mine(Ana)).Single().Name);
    }

    [Fact]
    public async Task AMember_CannotRenameTheOrganization()
    {
        await SignUpAnaAndBen();
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Send(Ben, HttpMethod.Put, $"/api/organizations/{Ana.OrganizationId}",
            new { name = "Ben Was Here" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ana's space", (await Mine(Ana)).Single().Name, ignoreCase: true);
    }

    [Fact]
    public async Task AnOutsider_CannotRenameAnotherOrganization()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ben, HttpMethod.Put, $"/api/organizations/{Ana.OrganizationId}",
            new { name = "Ben Was Here" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheDemoOrganization_CannotBeRenamed_NotEvenByItsOwner()
    {
        await SignUpAnaAndBen();
        await MakeDemo(Ana.OrganizationId);

        var response = await Send(Ana, HttpMethod.Put, $"/api/organizations/{Ana.OrganizationId}",
            new { name = "Renamed demo" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var mine = (await Mine(Ana)).Single();
        Assert.True(mine.IsDemo);
        Assert.NotEqual("Renamed demo", mine.Name);
    }

    // ---- DELETE /api/organizations/{id} ----

    [Fact]
    public async Task TheOwner_CanDeleteAnEmptyOrganization()
    {
        await SignUpAnaAndBen();
        var created = await Create(Ana, "Night Owls");

        var response = await Send(Ana, HttpMethod.Delete, $"/api/organizations/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await Mine(Ana), o => o.Id == created.Id);
        var after = await Send(Ana, HttpMethod.Get, $"/api/organizations/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task DeletingAnOrganization_RemovesItsMemberships_ButNotThePeople()
    {
        await SignUpAnaAndBen();
        await AddAsMember(Ben, Ana.OrganizationId);

        await Send(Ana, HttpMethod.Delete, $"/api/organizations/{Ana.OrganizationId}");

        // Ben lost access to Ana's organization, but his own account and organization are fine
        var bens = await Mine(Ben);
        Assert.Equal(Ben.OrganizationId, Assert.Single(bens).Id);
        var me = await Send(Ben, HttpMethod.Get, "/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task AnOrganizationWithProjects_CannotBeDeleted_UntilTheyAreGone()
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);

        var refused = await Send(Ana, HttpMethod.Delete, $"/api/organizations/{Ana.OrganizationId}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains(await Mine(Ana), o => o.Id == Ana.OrganizationId); // still there
        var projectStill = await Send(Ana, HttpMethod.Get, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, projectStill.StatusCode);

        await Send(Ana, HttpMethod.Delete, $"/api/projects/{project.Id}");
        var allowed = await Send(Ana, HttpMethod.Delete, $"/api/organizations/{Ana.OrganizationId}");
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task AMember_CannotDeleteTheOrganization()
    {
        await SignUpAnaAndBen();
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Send(Ben, HttpMethod.Delete, $"/api/organizations/{Ana.OrganizationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(await Mine(Ana), o => o.Id == Ana.OrganizationId);
    }

    [Fact]
    public async Task AnOutsider_CannotDeleteAnotherOrganization()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ben, HttpMethod.Delete, $"/api/organizations/{Ana.OrganizationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(await Mine(Ana), o => o.Id == Ana.OrganizationId);
    }

    [Fact]
    public async Task TheDemoOrganization_CannotBeDeleted_NotEvenByItsOwner()
    {
        await SignUpAnaAndBen();
        await MakeDemo(Ana.OrganizationId);

        var response = await Send(Ana, HttpMethod.Delete, $"/api/organizations/{Ana.OrganizationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(await Mine(Ana), o => o.Id == Ana.OrganizationId);
    }

    [Fact]
    public async Task RenamingWithAnInvalidName_Returns400()
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Put, $"/api/organizations/{Ana.OrganizationId}",
            new { name = "A" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
