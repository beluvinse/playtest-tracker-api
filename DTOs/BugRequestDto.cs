using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public abstract class BugRequestDto
    {
        public int? ProjectId { get; set; }

        [Required]
        [MinLength(3)]
        [MaxLength(BugReport.TitleMaxLength)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MinLength(10)]
        [MaxLength(BugReport.DescriptionMaxLength)]
        public string Description { get; set; } = string.Empty;

        [EnumDataType(typeof(Severity))]
        public Severity Severity { get; set; }
    }
}
