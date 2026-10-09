using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.Models
{
    public class BugReport
    {
        public const int TitleMaxLength = 200;
        public const int DescriptionMaxLength = 4000;

        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public int Id { get; set; }
        [MaxLength(TitleMaxLength)]
        public string Title { get; set; } = string.Empty;
        [MaxLength(DescriptionMaxLength)]
        public string Description { get; set; } = string.Empty;
        public Severity Severity { get; set; }
        public BugStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}