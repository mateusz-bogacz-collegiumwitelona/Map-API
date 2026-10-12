using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace Api.Factory
{
    public class ValidationResultFactory : IFluentValidationAutoValidationResultFactory
    {
        public Task<IActionResult?> CreateActionResult(
            ActionExecutingContext context,
            ValidationProblemDetails validationProblemDetails,
            IDictionary<IValidationContext, ValidationResult> validationResults)
        {
            var result = new BadRequestObjectResult(new
            {
                status = StatusCodes.Status400BadRequest,
                title = "Validation errors occurred.",
                errors = validationProblemDetails.Errors
            });

            return Task.FromResult<IActionResult?>(result);
        }
    }
}