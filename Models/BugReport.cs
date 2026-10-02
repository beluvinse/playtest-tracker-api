namespace PlaytestTracker.Api.Models
{
    public class BugReport
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Severity Severity { get; set; }
        public BugStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}