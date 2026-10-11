using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    // One person in an organization. Members of the same organization can see each other's
    // name and email; nothing private (never the password hash) ever goes in here.
    public class MemberDto
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public OrganizationRole Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }
}
