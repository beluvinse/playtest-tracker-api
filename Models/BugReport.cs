namespace PlaytestTracker.Api.Models
{
    public class BugReport
    {
        //public int ProjectId { get; set; }
        //public Project Project { get; set; } = null!;

        //TODO: sacarle el ? nnull mas adelante
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Severity Severity { get; set; }
        public BugStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}