using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public class UpdateBugDto : BugRequestDto
    {
        [EnumDataType(typeof(BugStatus))]
        public BugStatus Status { get; set; }
    }
}
