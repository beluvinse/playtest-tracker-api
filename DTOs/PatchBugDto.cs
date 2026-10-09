using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    // Every field is optional: null means "leave it as it is".
    // The validation attributes skip null values, so they only check the fields that were sent.
    public class PatchBugDto : IValidatableObject
    {
        public int? ProjectId { get; set; }

        [MinLength(3)]
        [MaxLength(BugReport.TitleMaxLength)]
        public string? Title { get; set; }

        [MinLength(10)]
        [MaxLength(BugReport.DescriptionMaxLength)]
        public string? Description { get; set; }

        [EnumDataType(typeof(Severity))]
        public Severity? Severity { get; set; }

        [EnumDataType(typeof(BugStatus))]
        public BugStatus? Status { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // An empty body is almost always a client mistake, like a misspelled field name
            if (ProjectId == null && Title == null && Description == null
                && Severity == null && Status == null)
            {
                yield return new ValidationResult("At least one field must be provided.");
            }
        }
    }
}
