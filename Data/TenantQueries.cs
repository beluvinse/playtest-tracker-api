using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Data
{
    // The one place that says what a person is allowed to see: only what belongs to an
    // organization they are a member of. Every query for projects or bugs starts from here,
    // so "forgetting the filter" can't happen by accident in one endpoint.
    public static class TenantQueries
    {
        public static IQueryable<Project> VisibleTo(this IQueryable<Project> projects, string userId)
            => projects.Where(project =>
                project.Organization.Members.Any(member => member.UserId == userId));

        // A bug has no organization of its own: it belongs to its project's
        public static IQueryable<BugReport> VisibleTo(this IQueryable<BugReport> bugs, string userId)
            => bugs.Where(bug =>
                bug.Project.Organization.Members.Any(member => member.UserId == userId));
    }
}
