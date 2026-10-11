using PlaytestTracker.Api.DTOs;

namespace PlaytestTracker.Api.Models
{
    public enum MemberOperationStatus
    {
        Success,

        // The organization doesn't exist or the person asking isn't in it (the same answer for both)
        OrganizationNotFound,

        // The person asking is a member, but their role doesn't allow it
        Forbidden,

        // The demo organization's people can't be changed by anybody
        DemoLocked,

        // Adding: nobody has an account with that email
        UserNotFound,

        // Adding: that person is already in the organization
        AlreadyMember,

        // Removing: the person isn't in the organization
        MemberNotFound,

        // Removing: the Owner is what holds the organization together, so it can't be removed
        OwnerCannotBeRemoved
    }

    public class MemberOperationResult
    {
        public MemberOperationStatus Status { get; set; }
        public MemberDto? Member { get; set; }
    }
}
