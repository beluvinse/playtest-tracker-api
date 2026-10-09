using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public class BugDto
    {
        public int Id { get; set; }
        public int? ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Severity Severity { get; set; }
        public BugStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
