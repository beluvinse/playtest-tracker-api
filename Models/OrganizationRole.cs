namespace PlaytestTracker.Api.Models
{
    // What a person can do inside an organization. The numbers are what the database stores,
    // so don't reorder them (the one-owner index in AppDbContext depends on Owner being 0).
    public enum OrganizationRole
    {
        // Runs the organization: adds and removes members, can delete it. Exactly one per organization.
        Owner = 0,

        // Works inside the organization: creates and edits projects and bugs
        Member = 1
    }
}
