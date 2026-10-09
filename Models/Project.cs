using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.Models
{
    public class Project
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 1000;

        public int Id { get; set; }
        [MaxLength(NameMaxLength)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(DescriptionMaxLength)]
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<BugReport> Bugs { get; set; } = new();
    }
}
