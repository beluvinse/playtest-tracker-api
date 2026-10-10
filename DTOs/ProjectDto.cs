namespace PlaytestTracker.Api.DTOs
{
    public class ProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int BugCount { get; set; }

        // Critical bugs that are not finished yet (Open or InProgress): the ones that need attention first
        public int OpenCriticalCount { get; set; }
    }
}
