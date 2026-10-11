using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Extensions;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // The people of one organization. An organization the person isn't in is answered 404,
    // exactly as if it didn't exist.
    // Everything here needs a valid token: without one, the request is answered 401 before any code runs
    [Authorize]
    [ApiController]
    [Route("api/organizations/{organizationId}/members")]
    public class OrganizationMembersController : ControllerBase
    {
        private readonly MembershipService _membershipService;

        public OrganizationMembersController(MembershipService membershipService)
        {
            _membershipService = membershipService;
        }

        [HttpGet]
        public async Task<ActionResult<List<MemberDto>>> GetAll(int organizationId)
        {
            var members = await _membershipService.GetMembersAsync(User.GetUserId(), organizationId);

            if (members == null)
                return NotFound();

            return Ok(members);
        }

        [HttpPost]
        public async Task<ActionResult<MemberDto>> Add(int organizationId, AddMemberDto dto)
        {
            var result = await _membershipService.AddAsync(User.GetUserId(), organizationId, dto.Email);

            return result.Status switch
            {
                MemberOperationStatus.OrganizationNotFound => NotFound(),
                MemberOperationStatus.DemoLocked => DemoLocked(),
                MemberOperationStatus.Forbidden => Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Only the owner can add people"),
                MemberOperationStatus.UserNotFound => FieldProblem(
                    StatusCodes.Status404NotFound, "No account with this email",
                    nameof(AddMemberDto.Email), "Nobody has signed up with this email yet."),
                MemberOperationStatus.AlreadyMember => FieldProblem(
                    StatusCodes.Status409Conflict, "Already a member",
                    nameof(AddMemberDto.Email), "This person is already in the organization."),
                _ => Created(
                    $"/api/organizations/{organizationId}/members",
                    result.Member)
            };
        }

        // The Owner removes a member, or a member removes themselves (leaves)
        [HttpDelete("{userId}")]
        public async Task<IActionResult> Remove(int organizationId, string userId)
        {
            var result = await _membershipService.RemoveAsync(User.GetUserId(), organizationId, userId);

            return result.Status switch
            {
                MemberOperationStatus.OrganizationNotFound or MemberOperationStatus.MemberNotFound => NotFound(),
                MemberOperationStatus.DemoLocked => DemoLocked(),
                MemberOperationStatus.Forbidden => Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Only the owner can remove other people",
                    detail: "You can only remove yourself."),
                MemberOperationStatus.OwnerCannotBeRemoved => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The owner can't be removed",
                    detail: "An organization always has its owner. Delete the organization instead."),
                _ => NoContent()
            };
        }

        private ObjectResult DemoLocked() => Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The demo organization can't be changed",
            detail: "It is there for everybody to look around in, so its people stay as they are.");

        // An error attached to one field, in the same "errors" format as validation errors,
        // so a form can show the message right under that field
        private ActionResult FieldProblem(int statusCode, string title, string field, string message)
        {
            ModelState.AddModelError(field, message);

            return ValidationProblem(
                statusCode: statusCode,
                title: title,
                modelStateDictionary: ModelState);
        }
    }
}
