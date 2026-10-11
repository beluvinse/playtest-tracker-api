namespace PlaytestTracker.Api.Models
{
    // "This person belongs to this organization, with this role". A person can have many
    // memberships (a personal space, a team, the demo) and an organization has many members,
    // so this table sits between the two.
    public class Membership
    {
        public int OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public OrganizationRole Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }
}
