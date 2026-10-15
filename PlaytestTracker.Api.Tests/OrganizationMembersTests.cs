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

// Who is in an organization: seeing them, adding them, removing them.
// Ana owns her organization. Ben, Cleo and Dan have accounts of their own.
public class OrganizationMembersTests : IsolationTestBase
{
    private Person _cleo = null!;
    private Person _dan = null!;

    private async Task SignUpEveryone()
    {
        await SignUpAnaAndBen();
        _cleo = await SignUp("cleo");
        _dan = await SignUp("dan");
    }

    private string Members(int organizationId) => $"/api/organizations/{organizationId}/members";

    private async Task<List<MemberDto>> MembersOf(Person person, int organizationId)
    {
        var response = await Send(person, HttpMethod.Get, Members(organizationId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResultDto<MemberDto>>(Json))!.Items;
    }

    private Task<HttpResponseMessage> AddByEmail(Person owner, int organizationId, string email)
        => Send(owner, HttpMethod.Post, Members(organizationId), new { email });

    private Task<HttpResponseMessage> Remove(Person person, int organizationId, Person target)
        => Send(person, HttpMethod.Delete, $"{Members(organizationId)}/{target.Id}");

    private async Task MakeDemo(int organizationId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Organizations.Where(o => o.Id == organizationId)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.IsDemo, true));
    }

    private async Task<bool> CanSeeOrganization(Person person, int organizationId)
    {
        var response = await Send(person, HttpMethod.Get, $"/api/organizations/{organizationId}");
        return response.StatusCode == HttpStatusCode.OK;
    }

    // ---- GET /members ----

    [Fact]
    public async Task AMember_SeesEveryoneInTheOrganization_TheOwnerFirst()
    {
        await SignUpEveryone();
        await AddAsMember(_cleo, Ana.OrganizationId);
        await AddAsMember(Ben, Ana.OrganizationId);

        var members = await MembersOf(Ben, Ana.OrganizationId);

        Assert.Equal(3, members.Count);
        Assert.Equal(Ana.Id, members[0].UserId);
        Assert.Equal(OrganizationRole.Owner, members[0].Role);
        Assert.Equal("ana@test.local", members[0].Email);
        Assert.Equal("ana", members[0].DisplayName);
        Assert.All(members.Skip(1), member => Assert.Equal(OrganizationRole.Member, member.Role));
    }

    // ---- GET /members is paged ----

    // Ana owns an organization with eleven more people in it: twelve in total
    private async Task AnaWithElevenMembers()
    {
        await SignUpAnaAndBen();
        for (var i = 1; i <= 11; i++)
            await AddAsMember(await SignUp($"member{i:00}"), Ana.OrganizationId);
    }

    private async Task<PagedResultDto<MemberDto>> MembersPage(Person person, int organizationId, string query)
    {
        var response = await Send(person, HttpMethod.Get, $"{Members(organizationId)}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResultDto<MemberDto>>(Json))!;
    }

    [Fact]
    public async Task TheList_IsCutIntoPages_TheOwnerOnTheFirstOne()
    {
        await AnaWithElevenMembers();

        var first = await MembersPage(Ana, Ana.OrganizationId, "?page=1&pageSize=5");
        var second = await MembersPage(Ana, Ana.OrganizationId, "?page=2&pageSize=5");
        var third = await MembersPage(Ana, Ana.OrganizationId, "?page=3&pageSize=5");

        Assert.Equal([5, 5, 2], [first.Items.Count, second.Items.Count, third.Items.Count]);
        Assert.All(new[] { first, second, third }, page => Assert.Equal(12, page.TotalCount));
        Assert.Equal((2, 5), (second.Page, second.PageSize));
        Assert.Equal(Ana.Id, first.Items[0].UserId);
        Assert.Equal(OrganizationRole.Owner, first.Items[0].Role);
    }

    [Fact]
    public async Task EveryPersonAppearsOnExactlyOnePage()
    {
        await AnaWithElevenMembers();

        var everybody = new List<string>();
        for (var page = 1; page <= 3; page++)
            everybody.AddRange((await MembersPage(Ana, Ana.OrganizationId, $"?page={page}&pageSize=5"))
                .Items.Select(member => member.UserId));

        Assert.Equal(12, everybody.Count);
        Assert.Equal(12, everybody.Distinct().Count());
    }

    [Fact]
    public async Task WithoutAskingForAPage_TheFirstTenAreSent()
    {
        await AnaWithElevenMembers();

        var page = await MembersPage(Ana, Ana.OrganizationId, "");

        Assert.Equal((1, 10), (page.Page, page.PageSize));
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(12, page.TotalCount);
    }

    [Fact]
    public async Task APagePastTheEnd_IsEmpty_ButSaysHowManyThereAre()
    {
        await AnaWithElevenMembers();

        var page = await MembersPage(Ana, Ana.OrganizationId, "?page=9&pageSize=10");

        Assert.Empty(page.Items);
        Assert.Equal(12, page.TotalCount);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=51")]
    public async Task AnInvalidPageOrSize_IsRefused(string query)
    {
        await SignUpAnaAndBen();

        var response = await Send(Ana, HttpMethod.Get, $"{Members(Ana.OrganizationId)}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FiftyPerPage_IsTheMostAllowed()
    {
        await SignUpAnaAndBen();

        var page = await MembersPage(Ana, Ana.OrganizationId, "?pageSize=50");

        Assert.Equal(50, page.PageSize);
    }

    [Fact]
    public async Task AnOutsider_GetsNothing_WhateverPageTheyAskFor()
    {
        await AnaWithElevenMembers();

        var response = await Send(Ben, HttpMethod.Get, $"{Members(Ana.OrganizationId)}?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheList_NeverContainsPasswordsOrHashes()
    {
        await SignUpEveryone();

        var response = await Send(Ana, HttpMethod.Get, Members(Ana.OrganizationId));

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sunflower7", text);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnOutsider_CannotSeeWhoIsInAnOrganization()
    {
        await SignUpEveryone();

        var response = await Send(Ben, HttpMethod.Get, Members(Ana.OrganizationId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- POST /members ----

    [Fact]
    public async Task TheOwner_CanAddSomeoneByEmail_AndTheyGetAccessToEverything()
    {
        await SignUpEveryone();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        Assert.False(await CanSeeOrganization(Ben, Ana.OrganizationId));

        var response = await AddByEmail(Ana, Ana.OrganizationId, "ben@test.local");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var added = (await response.Content.ReadFromJsonAsync<MemberDto>(Json))!;
        Assert.Equal(Ben.Id, added.UserId);
        Assert.Equal(OrganizationRole.Member, added.Role);

        Assert.True(await CanSeeOrganization(Ben, Ana.OrganizationId));
        var projects = await Send(Ben, HttpMethod.Get, $"/api/organizations/{Ana.OrganizationId}/projects");
        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);
        var seen = await Send(Ben, HttpMethod.Get, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
        Assert.Equal(2, (await MembersOf(Ana, Ana.OrganizationId)).Count);
    }

    [Theory]
    [InlineData("BEN@TEST.LOCAL")]
    [InlineData("  ben@test.local  ")]
    public async Task TheEmail_IsFoundWhateverTheCapitalsOrSpaces(string email)
    {
        await SignUpEveryone();

        var response = await AddByEmail(Ana, Ana.OrganizationId, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddingAnEmailNobodyHas_Returns404WithTheMessageOnTheEmailField()
    {
        await SignUpEveryone();

        var response = await AddByEmail(Ana, Ana.OrganizationId, "nobody@test.local");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Single(await MembersOf(Ana, Ana.OrganizationId)); // nothing was added
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task AddingSomethingThatIsNotAnEmail_Returns400(string email)
    {
        await SignUpEveryone();

        var response = await AddByEmail(Ana, Ana.OrganizationId, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddingSomeoneWhoIsAlreadyIn_Returns409()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);

        var again = await AddByEmail(Ana, Ana.OrganizationId, "ben@test.local");
        var theOwner = await AddByEmail(Ana, Ana.OrganizationId, "ana@test.local");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, theOwner.StatusCode);
        Assert.Equal(2, (await MembersOf(Ana, Ana.OrganizationId)).Count);
    }

    [Fact]
    public async Task AMember_CannotAddPeople()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await AddByEmail(Ben, Ana.OrganizationId, "cleo@test.local");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await CanSeeOrganization(_cleo, Ana.OrganizationId));
    }

    [Fact]
    public async Task AnOutsider_CannotAddThemselvesOrAnyoneElse()
    {
        await SignUpEveryone();

        var response = await AddByEmail(Ben, Ana.OrganizationId, "ben@test.local");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await CanSeeOrganization(Ben, Ana.OrganizationId));
    }

    [Fact]
    public async Task InTheDemoOrganization_NobodyCanBeAdded_NotEvenByTheOwner()
    {
        await SignUpEveryone();
        await MakeDemo(Ana.OrganizationId);

        var response = await AddByEmail(Ana, Ana.OrganizationId, "ben@test.local");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(await MembersOf(Ana, Ana.OrganizationId));
    }

    // ---- DELETE /members/{userId} ----

    [Fact]
    public async Task TheOwner_CanRemoveAMember_WhoThenLosesAccess()
    {
        await SignUpEveryone();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Remove(Ana, Ana.OrganizationId, Ben);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await CanSeeOrganization(Ben, Ana.OrganizationId));
        var projectAfter = await Send(Ben, HttpMethod.Get, $"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, projectAfter.StatusCode);
        Assert.Single(await MembersOf(Ana, Ana.OrganizationId));
    }

    [Fact]
    public async Task ARemovedPerson_CanBeAddedAgain()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);
        await Remove(Ana, Ana.OrganizationId, Ben);

        var response = await AddByEmail(Ana, Ana.OrganizationId, "ben@test.local");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AMember_CanLeaveTheOrganization()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Remove(Ben, Ana.OrganizationId, Ben);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await CanSeeOrganization(Ben, Ana.OrganizationId));
        Assert.True(await CanSeeOrganization(Ben, Ben.OrganizationId)); // his own is still his
    }

    [Fact]
    public async Task AMember_CannotRemoveAnotherMember()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);
        await AddAsMember(_cleo, Ana.OrganizationId);

        var response = await Remove(Ben, Ana.OrganizationId, _cleo);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(3, (await MembersOf(Ana, Ana.OrganizationId)).Count);
    }

    [Fact]
    public async Task AMember_CannotRemoveTheOwner()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Remove(Ben, Ana.OrganizationId, Ana);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(OrganizationRole.Owner, (await MembersOf(Ana, Ana.OrganizationId))[0].Role);
    }

    [Fact]
    public async Task TheOwner_CannotRemoveThemselves_SoTheOrganizationNeverLosesItsOwner()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);

        var response = await Remove(Ana, Ana.OrganizationId, Ana);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var members = await MembersOf(Ana, Ana.OrganizationId);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, member => member.UserId == Ana.Id && member.Role == OrganizationRole.Owner);
    }

    [Fact]
    public async Task RemovingSomeoneWhoIsNotInTheOrganization_Returns404()
    {
        await SignUpEveryone();

        var response = await Remove(Ana, Ana.OrganizationId, _dan);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnOutsider_CannotRemoveAnyone()
    {
        await SignUpEveryone();
        await AddAsMember(_cleo, Ana.OrganizationId);

        var response = await Remove(Ben, Ana.OrganizationId, _cleo);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(2, (await MembersOf(Ana, Ana.OrganizationId)).Count);
    }

    [Fact]
    public async Task InTheDemoOrganization_NobodyCanBeRemoved_NotEvenByLeaving()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);
        await MakeDemo(Ana.OrganizationId);

        var byOwner = await Remove(Ana, Ana.OrganizationId, Ben);
        var byThemselves = await Remove(Ben, Ana.OrganizationId, Ben);

        Assert.Equal(HttpStatusCode.Forbidden, byOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byThemselves.StatusCode);
        Assert.Equal(2, (await MembersOf(Ana, Ana.OrganizationId)).Count);
    }

    // ---- The database rule behind "one owner" ----

    [Fact]
    public async Task EveryOrganization_KeepsExactlyOneOwner_WhateverHappens()
    {
        await SignUpEveryone();
        await AddAsMember(Ben, Ana.OrganizationId);
        await AddByEmail(Ana, Ana.OrganizationId, "cleo@test.local");
        await Remove(Ana, Ana.OrganizationId, Ben);
        await Remove(_cleo, Ana.OrganizationId, _cleo);
        await Remove(Ana, Ana.OrganizationId, Ana); // refused

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownersPerOrganization = await db.Memberships
            .Where(m => m.Role == OrganizationRole.Owner)
            .GroupBy(m => m.OrganizationId)
            .Select(g => g.Count())
            .ToListAsync();
        var organizations = await db.Organizations.CountAsync();

        Assert.Equal(organizations, ownersPerOrganization.Count);
        Assert.All(ownersPerOrganization, count => Assert.Equal(1, count));
    }
}
