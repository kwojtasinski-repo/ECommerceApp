using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Filters
{
    /// <summary>
    /// Global async action filter that runs FluentValidation for any action argument
    /// bound from [FromBody] or [FromForm]. Returns 400 with validation error details
    /// if validation fails. Passes through if no IValidator&lt;T&gt; is registered for the type.
    ///
    /// Registered globally in Startup.ConfigureServices — replaces AddFluentValidationAutoValidation()
    /// for the API project so that FluentValidation.AspNetCore is not required here.
    /// </summary>
    public sealed class FluentValidationFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var services = context.HttpContext.RequestServices;

            foreach (var (_, value) in context.ActionArguments)
            {
                if (value is null) continue;

                var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
                if (services.GetService(validatorType) is not IValidator validator) continue;

                var validationContext = new ValidationContext<object>(value);
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

                if (!result.IsValid)
                {
                    var errors = result.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray());

                    context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
                    return;
                }
            }

            await next();
        }
    }
}
