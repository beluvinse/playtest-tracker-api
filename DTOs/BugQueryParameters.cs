using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public class BugQueryParameters
    {
        public int? ProjectId { get; set; }

        [EnumDataType(typeof(BugStatus))]
        public BugStatus? Status { get; set; }

        [EnumDataType(typeof(Severity))]
        public Severity? Severity { get; set; }

        public string? Search { get; set; }

        [EnumDataType(typeof(BugSortField))]
        public BugSortField? SortBy { get; set; }

        public bool Descending { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 50)]
        public int PageSize { get; set; } = 10;
    }
}
