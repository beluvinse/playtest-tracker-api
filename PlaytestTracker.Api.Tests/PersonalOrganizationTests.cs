using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// Everybody who signs up gets an organization of their own, where they are the Owner
public class PersonalOrganizationTests : IDisposable
{
    private const string Password = "Sunflower7";

    private readonly TestApp _app = new();
    private readonly AuthService _auth;
    private readonly AppDbContext _db;

    public PersonalOrganizationTests()
    {
        _auth = _app.Get<AuthService>();
        _db = _app.Get<AppDbContext>();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Register_CreatesAPersonalOrganization_WhereThePersonIsTheOwner()
    {
        var result = await _auth.RegisterAsync("ana@test.local", Password, "Ana");

        var membership = await _db.Memberships
            .Include(m => m.Organization)
            .SingleAsync();
        Assert.Equal(result.User!.Id, membership.UserId);
        Assert.Equal(OrganizationRole.Owner, membership.Role);
        Assert.Equal("Ana's space", membership.Organization.Name);
        Assert.False(membership.Organization.IsDemo);
    }

    [Fact]
    public async Task Register_TwoPeople_GetTwoSeparateOrganizations()
    {
        var ana = (await _auth.RegisterAsync("ana@test.local", Password, "Ana")).User!;
        var ben = (await _auth.RegisterAsync("ben@test.local", Password, "Ben")).User!;

        Assert.Equal(2, await _db.Organizations.CountAsync());
        var anaOrganization = await _db.Memberships.SingleAsync(m => m.UserId == ana.Id);
        var benOrganization = await _db.Memberships.SingleAsync(m => m.UserId == ben.Id);
        Assert.NotEqual(anaOrganization.OrganizationId, benOrganization.OrganizationId);
    }

    [Theory]
    [InlineData("ana@test.local", "short")]  // password too weak
    [InlineData("not-an-email", Password)]   // not an email
    public async Task Register_ThatFails_CreatesNoOrganization(string email, string password)
    {
        await _auth.RegisterAsync(email, password, "Ana");

        Assert.Empty(await _db.Organizations.ToListAsync());
        Assert.Empty(await _db.Memberships.ToListAsync());
    }

    [Fact]
    public async Task Register_WithAnEmailAlreadyUsed_CreatesNoSecondOrganization()
    {
        await _auth.RegisterAsync("ana@test.local", Password, "Ana");

        await _auth.RegisterAsync("ana@test.local", Password, "Ana again");

        Assert.Equal(1, await _db.Organizations.CountAsync());
    }

    [Fact]
    public async Task Register_IfTheOrganizationCannotBeCreated_NoAccountIsLeftBehind()
    {
        // A clock that breaks when asked the time. The organization is created AFTER the account
        // and needs the time, so this makes the second step fail once the first has already worked.
        using var app = new TestApp(clock: new BrokenClock());
        var auth = app.Get<AuthService>();
        var db = app.Get<AppDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => auth.RegisterAsync("ana@test.local", Password, "Ana"));

        // Without the transaction, the account would still be here, with no organization
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Users.ToListAsync());
    }

    private sealed class BrokenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("The clock is broken");
    }
}
