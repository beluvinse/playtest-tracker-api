using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Extensions;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    // The organizations of the signed-in person. One they aren't a member of is answered 404,
    // exactly as if it didn't exist.
    // Everything here needs a valid token: without one, the request is answered 401 before any code runs
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrganizationsController : ControllerBase
    {
        private readonly OrganizationService _organizationService;

        public OrganizationsController(OrganizationService organizationService)
        {
            _organizationService = organizationService;
        }

        // "My organizations": what the frontend shows in its switcher
        [HttpGet]
        public async Task<ActionResult<List<OrganizationDto>>> GetMine()
        {
            return Ok(await _organizationService.GetMineAsync(User.GetUserId()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrganizationDto>> GetById(int id)
        {
            var organization = await _organizationService.GetByIdAsync(User.GetUserId(), id);

            if (organization == null)
                return NotFound();

            return Ok(organization);
        }

        [HttpPost]
        public async Task<ActionResult<OrganizationDto>> Create(OrganizationRequestDto dto)
        {
            var organization = await _organizationService.CreateAsync(User.GetUserId(), dto.Name.Trim());

            return CreatedAtAction(nameof(GetById), new { id = organization.Id }, organization);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<OrganizationDto>> Rename(int id, OrganizationRequestDto dto)
        {
            var result = await _organizationService.RenameAsync(User.GetUserId(), id, dto.Name.Trim());

            return result.Status switch
            {
                OrganizationOperationStatus.NotFound => NotFound(),
                OrganizationOperationStatus.DemoLocked => DemoLocked(),
                OrganizationOperationStatus.Forbidden => Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Only the owner can rename the organization"),
                _ => Ok(result.Organization)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _organizationService.DeleteAsync(User.GetUserId(), id);

            return result.Status switch
            {
                OrganizationOperationStatus.NotFound => NotFound(),
                OrganizationOperationStatus.DemoLocked => DemoLocked(),
                OrganizationOperationStatus.Forbidden => Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Only the owner can delete the organization"),
                OrganizationOperationStatus.HasProjects => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Organization has projects",
                    detail: "Delete its projects before deleting the organization."),
                _ => NoContent()
            };
        }

        private ObjectResult DemoLocked() => Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The demo organization can't be changed",
            detail: "It is there for everybody to look around in, so it stays as it is.");
    }
}
