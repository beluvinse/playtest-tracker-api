using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.DTOs
{
    // The query string of GET /api/organizations/{id}/members: ?page=2&pageSize=10.
    // The demo organization gets a new member with every sign-up, so its list can't be sent whole.
    public class MemberListParameters
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 50)]
        public int PageSize { get; set; } = 10;
    }
}
