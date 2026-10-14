using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// GET /api/demo: public information so the sign-in page can offer the demo accounts
public class DemoInfoTests : IsolationTestBase
{
    public DemoInfoTests() : base(demo: true) { }

    // No token at all: this is what a person who hasn't signed in sees
    private async Task<HttpResponseMessage> AskWithoutToken() => await Client.GetAsync("/api/demo");

    [Fact]
    public async Task WithoutSigningIn_TheDemoAccountsAndPasswordAreAvailable()
    {
        await SeedTheDemo();

        var response = await AskWithoutToken();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = (await response.Content.ReadFromJsonAsync<DemoInfoDto>(Json))!;
        Assert.Equal("Pink in the Night Studio", info.OrganizationName);
        Assert.Equal(TestApp.DemoPassword, info.Password);
        Assert.Equal(5, info.Accounts.Count);
        Assert.Equal("luna@demo.test", info.Accounts[0].Email);
        Assert.Equal(OrganizationRole.Owner, info.Accounts[0].Role);
        Assert.All(info.Accounts.Skip(1), account => Assert.Equal(OrganizationRole.Member, account.Role));
    }

    [Fact]
    public async Task ItSaysWhichOrganizationTheDemoIs_SoTheAppCanTakeSomeoneStraightToIt()
    {
        await SeedTheDemo();
        var info = (await (await AskWithoutToken()).Content.ReadFromJsonAsync<DemoInfoDto>(Json))!;

        // A demo account signs in and asks for that very organization: it is theirs, it is the demo
        var login = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = info.Accounts[0].Email, password = info.Password });
        var token = (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token;
        var organization = await Send(
            new Person(token, Id: "", OrganizationId: 0), HttpMethod.Get, $"/api/organizations/{info.OrganizationId}");

        Assert.Equal(HttpStatusCode.OK, organization.StatusCode);
        var body = (await organization.Content.ReadFromJsonAsync<OrganizationDto>(Json))!;
        Assert.True(body.IsDemo);
        Assert.Equal(info.OrganizationName, body.Name);
    }

    [Fact]
    public async Task TheDemosIdIsNotTheOneOfAPersonalOrganization()
    {
        await SeedTheDemo();
        var visitor = await SignUp("visitor"); // gets a personal organization too

        var info = (await (await AskWithoutToken()).Content.ReadFromJsonAsync<DemoInfoDto>(Json))!;

        Assert.NotEqual(visitor.OrganizationId, info.OrganizationId);
    }

    [Fact]
    public async Task EveryAccountItListsReallyWorks_WithThePasswordItGives()
    {
        await SeedTheDemo();
        var info = (await (await AskWithoutToken()).Content.ReadFromJsonAsync<DemoInfoDto>(Json))!;

        foreach (var account in info.Accounts)
        {
            var login = await Client.PostAsJsonAsync("/api/auth/login",
                new { email = account.Email, password = info.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
    }

    [Fact]
    public async Task VisitorsWhoJoinedTheDemo_AreNotListedAsDemoAccounts()
    {
        await SeedTheDemo();
        await SignUp("visitor");

        var info = (await (await AskWithoutToken()).Content.ReadFromJsonAsync<DemoInfoDto>(Json))!;

        Assert.Equal(5, info.Accounts.Count);
        Assert.DoesNotContain(info.Accounts, account => account.Email.Contains("visitor"));
    }

    [Fact]
    public async Task ItNeverShowsAnythingAboutRealOrganizationsOrPeople()
    {
        await SeedTheDemo();
        await SignUp("ana");

        var text = await (await AskWithoutToken()).Content.ReadAsStringAsync();

        Assert.DoesNotContain("ana@test.local", text);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenTheDemoWasNotCreatedYet_Returns404()
    {
        var response = await AskWithoutToken();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

// With the demo off, there is nothing to tell, even if a demo organization is still in the database
public class DemoInfoOffTests : IsolationTestBase
{
    [Fact]
    public async Task WhenTheDemoIsOff_Returns404()
    {
        var someone = await SignUp("someone");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Organizations.Where(o => o.Id == someone.OrganizationId)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.IsDemo, true));
        }

        var response = await Client.GetAsync("/api/demo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
