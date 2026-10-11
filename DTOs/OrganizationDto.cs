using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    // An organization as the person asking sees it: what it is, and what THEY are in it
    public class OrganizationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // The example organization anyone can look around in; it can't be changed or deleted
        public bool IsDemo { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        // The role of the person who asked (Owner or Member)
        public OrganizationRole MyRole { get; set; }

        public int MemberCount { get; set; }
        public int ProjectCount { get; set; }
    }
}
