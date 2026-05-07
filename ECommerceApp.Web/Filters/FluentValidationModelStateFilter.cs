using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Filters
{
    /// <summary>
    /// Global async action filter that runs FluentValidation for any action argument
    /// and adds validation failures into <see cref="Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary"/>.
    ///
    /// The existing <see cref="ModelStateFilter"/> then short-circuits with 400 when ModelState is invalid,
    /// and Web MVC controllers can still use <c>if (!ModelState.IsValid)</c> to re-render the view.
    ///
    /// This replaces <c>AddFluentValidationAutoValidation()</c> from FluentValidation.AspNetCore,
    /// keeping the Web project free of that deprecated ASP.NET-specific package.
    /// </summary>
    public sealed class FluentValidationModelStateFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var services = context.HttpContext.RequestServices;

            foreach (var (key, value) in context.ActionArguments)
            {
                if (value is null) continue;

                var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
                if (services.GetService(validatorType) is not IValidator validator) continue;

                var validationContext = new ValidationContext<object>(value);
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

                if (!result.IsValid)
                {
                    foreach (var error in result.Errors)
                    {
                        context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                }
            }

            // Always continue — ModelStateFilter (already global) will short-circuit on invalid ModelState.
            // This also allows controller actions to re-render the view with validation messages.
            await next();
        }
    }
}
