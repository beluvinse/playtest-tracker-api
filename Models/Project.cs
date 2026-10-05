namespace PlaytestTracker.Api.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<BugReport> Bugs { get; set; } = new();
    }
}
