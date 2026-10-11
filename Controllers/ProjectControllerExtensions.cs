using Microsoft.AspNetCore.Mvc;
using PlaytestTracker.Api.DTOs;

namespace PlaytestTracker.Api.Controllers
{
    // Shared by the two controllers that save projects
    public static class ProjectControllerExtensions
    {
        // 409 Conflict with the message attached to the "Code" field, in the same "errors"
        // format as a validation error, so a form can show it right under the code input
        public static ActionResult CodeTaken(this ControllerBase controller, string code)
        {
            controller.ModelState.AddModelError(
                nameof(ProjectRequestDto.Code),
                $"Another project in this organization already uses the code {code}.");

            return controller.ValidationProblem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Code already in use",
                modelStateDictionary: controller.ModelState);
        }
    }
}
