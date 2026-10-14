using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// The rest of the tests run on SQLite. These run on a real PostgreSQL, because they check what only
// the real database can answer: the migrations apply, the database itself refuses what the code
// expects it to refuse, and text search ignores capitals. Without PLAYTEST_TEST_POSTGRES they are skipped.
public class PostgresTests
{
    private static ApplicationUser NewUser(string name) => new()
    {
        Id = name,
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        Email = $"{name}@example.test",
        NormalizedEmail = $"{name}@example.test".ToUpperInvariant(),
        DisplayName = name
    };

    // An organization with its Owner, ready to use
    private static async Task<int> CreateOrganizationAsync(AppDbContext context, ApplicationUser owner)
    {
        context.Users.Add(owner);
        var organization = new Organization { Name = "Studio", CreatedAt = DateTimeOffset.UtcNow };
        organization.Members.Add(new Membership
        {
            UserId = owner.Id,
            Role = OrganizationRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow
        });
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization.Id;
    }

    [PostgresFact]
    public async Task The_migrations_leave_the_database_matching_the_model()
    {
        using var database = new PostgresDatabase();
        using var context = database.NewContext();

        // If a model change was made without a migration, EF knows there are changes pending
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [PostgresFact]
    public async Task A_project_code_taken_in_the_same_instant_is_answered_as_taken()
    {
        using var database = new PostgresDatabase();
        int organizationId;
        using (var setup = database.NewContext())
            organizationId = await CreateOrganizationAsync(setup, NewUser("ana"));

        // The service checks the code is free, and right before saving, somebody else takes it.
        // Only the database can stop the second one now (the unique index on organization + code).
        var intruder = database.NewContext();
        var race = new BeforeFirstSave(() =>
        {
            intruder.Projects.Add(new Project { OrganizationId = organizationId, Name = "First", Code = "PIN", CreatedAt = DateTimeOffset.UtcNow });
            intruder.SaveChanges();
        });
        using var context = database.NewContext(race);

        var result = await new ProjectService(context).AddAsync(
            "ana", organizationId, new Project { Name = "Second", Code = "PIN" });

        Assert.Equal(ProjectOperationStatus.CodeTaken, result.Status);

        using var check = database.NewContext();
        Assert.Equal(1, await check.Projects.CountAsync());
    }

    [PostgresFact]
    public async Task A_person_added_in_the_same_instant_is_answered_as_already_a_member()
    {
        using var database = new PostgresDatabase();
        int organizationId;
        using (var setup = database.NewContext())
        {
            organizationId = await CreateOrganizationAsync(setup, NewUser("ana"));
            setup.Users.Add(NewUser("ben"));
            await setup.SaveChangesAsync();
        }

        var intruder = database.NewContext();
        var race = new BeforeFirstSave(() =>
        {
            intruder.Memberships.Add(new Membership
            {
                OrganizationId = organizationId,
                UserId = "ben",
                Role = OrganizationRole.Member,
                JoinedAt = DateTimeOffset.UtcNow
            });
            intruder.SaveChanges();
        });
        using var context = database.NewContext(race);

        var result = await new MembershipService(context, TimeProvider.System).AddAsync(
            "ana", organizationId, "ben@example.test");

        Assert.Equal(MemberOperationStatus.AlreadyMember, result.Status);
    }

    [PostgresFact]
    public async Task The_database_itself_allows_only_one_owner_per_organization()
    {
        using var database = new PostgresDatabase();
        using var context = database.NewContext();
        var organizationId = await CreateOrganizationAsync(context, NewUser("ana"));
        context.Users.Add(NewUser("ben"));
        await context.SaveChangesAsync();

        context.Memberships.Add(new Membership
        {
            OrganizationId = organizationId,
            UserId = "ben",
            Role = OrganizationRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow
        });

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.True(failure.IsDuplicateKey());
    }

    [PostgresFact]
    public async Task Any_number_of_members_can_share_an_organization_with_its_owner()
    {
        // The other side of the index above: it only limits Owners, not Members
        using var database = new PostgresDatabase();
        using var context = database.NewContext();
        var organizationId = await CreateOrganizationAsync(context, NewUser("ana"));

        foreach (var name in new[] { "ben", "cat", "dan" })
        {
            context.Users.Add(NewUser(name));
            context.Memberships.Add(new Membership
            {
                OrganizationId = organizationId,
                UserId = name,
                Role = OrganizationRole.Member,
                JoinedAt = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync();
        Assert.Equal(4, await context.Memberships.CountAsync());
    }

    [PostgresFact]
    public async Task Search_ignores_capitals_in_titles_and_descriptions()
    {
        using var database = new PostgresDatabase();
        using var context = database.NewContext();
        var organizationId = await CreateOrganizationAsync(context, NewUser("ana"));

        var project = new Project { OrganizationId = organizationId, Name = "Game", Code = "GAM", CreatedAt = DateTimeOffset.UtcNow };
        project.Bugs.Add(new BugReport { Title = "Neon Sign flickers", Description = "x", CreatedAt = DateTimeOffset.UtcNow });
        project.Bugs.Add(new BugReport { Title = "Crash", Description = "Happens near the NEON door", CreatedAt = DateTimeOffset.UtcNow });
        project.Bugs.Add(new BugReport { Title = "Unrelated", Description = "y", CreatedAt = DateTimeOffset.UtcNow });
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var found = await new BugService(context).GetAllAsync("ana", new BugListParameters { Search = "neon" });

        Assert.Equal(2, found.TotalCount);
    }

    [PostgresFact]
    public async Task Dates_come_back_as_the_same_instant()
    {
        using var database = new PostgresDatabase();
        var moment = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        int organizationId;
        using (var setup = database.NewContext())
        {
            organizationId = await CreateOrganizationAsync(setup, NewUser("ana"));
            setup.Projects.Add(new Project { OrganizationId = organizationId, Name = "P", Code = "PPP", CreatedAt = moment });
            await setup.SaveChangesAsync();
        }

        using var context = database.NewContext();
        var project = await context.Projects.SingleAsync();

        Assert.Equal(moment, project.CreatedAt);
        Assert.Equal(TimeSpan.Zero, project.CreatedAt.Offset);
    }
}
