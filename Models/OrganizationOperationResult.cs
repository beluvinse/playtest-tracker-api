using PlaytestTracker.Api.DTOs;

namespace PlaytestTracker.Api.Models
{
    public enum OrganizationOperationStatus
    {
        Success,

        // It doesn't exist, or the person isn't a member of it (the same answer for both)
        NotFound,

        // The person is a member, but their role doesn't allow it
        Forbidden,

        // The demo organization can't be changed by anybody
        DemoLocked
    }

    public class OrganizationOperationResult
    {
        public OrganizationOperationStatus Status { get; set; }
        public OrganizationDto? Organization { get; set; }
    }
}
