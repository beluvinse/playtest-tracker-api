using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public class UpdateBugDto
    {
        public int? ProjectId { get; set; }

        [Required]
        [MinLength(3)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MinLength(10)]
        public string Description { get; set; } = string.Empty;

        [EnumDataType(typeof(Severity))]
        public Severity Severity { get; set; }

        [EnumDataType(typeof(BugStatus))]
        public BugStatus Status { get; set; }
    }
}
