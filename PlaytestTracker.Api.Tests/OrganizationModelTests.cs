using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// These tests check the rules the DATABASE enforces, not the ones in our code: they save things
// directly and expect the database to refuse the ones that break a rule.
public class OrganizationModelTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly AppDbContext _db;

    public OrganizationModelTests() => _db = _app.Get<AppDbContext>();

    public void Dispose() => _app.Dispose();

    private async Task<ApplicationUser> NewUser(string name)
    {
        // Saved directly, not through registration: registering also creates a personal
        // organization, and these tests need to start with none
        var user = new ApplicationUser
        {
            UserName = $"{name}@test.local",
            Email = $"{name}@test.local",
            DisplayName = name
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> NewOrganization(string name = "Team")
    {
        var organization = new Organization { Name = name, CreatedAt = DateTimeOffset.UtcNow };
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync();
        return organization;
    }

    private Task Join(Organization organization, ApplicationUser user, OrganizationRole role)
    {
        _db.Memberships.Add(new Membership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow
        });
        return _db.SaveChangesAsync();
    }

    [Fact]
    public async Task AnOrganization_CanHaveOneOwnerAndManyMembers()
    {
        var organization = await NewOrganization();
        await Join(organization, await NewUser("owner"), OrganizationRole.Owner);
        await Join(organization, await NewUser("one"), OrganizationRole.Member);
        await Join(organization, await NewUser("two"), OrganizationRole.Member);

        var roles = await _db.Memberships
            .Where(m => m.OrganizationId == organization.Id)
            .Select(m => m.Role)
            .ToListAsync();

        Assert.Equal(1, roles.Count(role => role == OrganizationRole.Owner));
        Assert.Equal(2, roles.Count(role => role == OrganizationRole.Member));
    }

    [Fact]
    public async Task AnOrganization_CannotHaveASecondOwner()
    {
        var organization = await NewOrganization();
        await Join(organization, await NewUser("first"), OrganizationRole.Owner);

        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await Join(organization, await NewUser("second"), OrganizationRole.Owner));
    }

    [Fact]
    public async Task APerson_CanOwnOneOrganizationAndBeAMemberOfAnother()
    {
        var ana = await NewUser("ana");
        var personal = await NewOrganization("Ana's space");
        var team = await NewOrganization("Team");
        await Join(personal, ana, OrganizationRole.Owner);
        await Join(team, await NewUser("boss"), OrganizationRole.Owner);

        await Join(team, ana, OrganizationRole.Member);

        Assert.Equal(2, await _db.Memberships.CountAsync(m => m.UserId == ana.Id));
    }

    [Fact]
    public async Task APerson_CannotJoinTheSameOrganizationTwice()
    {
        var organization = await NewOrganization();
        var ana = await NewUser("ana");
        await Join(organization, ana, OrganizationRole.Member);

        // EF keeps what it saved in memory and would refuse the duplicate itself, before the
        // database sees it. Forgetting it makes the DATABASE the one that has to say no.
        _db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await Join(organization, ana, OrganizationRole.Member));
    }

    [Fact]
    public async Task DeletingAnOrganization_DeletesItsMemberships_ButNotThePeople()
    {
        var organization = await NewOrganization();
        var ana = await NewUser("ana");
        await Join(organization, ana, OrganizationRole.Owner);

        _db.Organizations.Remove(organization);
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.Memberships.ToListAsync());
        Assert.True(await _db.Users.AnyAsync(user => user.Id == ana.Id));
    }
}
