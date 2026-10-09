namespace PlaytestTracker.Api.DTOs
{
    // Query parameters for GET /api/bugs, which can search across every project
    public class BugQueryParameters : BugListParameters
    {
        public int? ProjectId { get; set; }
    }
}
