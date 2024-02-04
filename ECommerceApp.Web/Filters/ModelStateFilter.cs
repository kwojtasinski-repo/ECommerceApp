using ECommerceApp.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ECommerceApp.Web.Filters
{
    public class ModelStateFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            SetUnvalidatedAsValidated(context.ModelState);
            if (!context.ModelState.IsValid)
            {
                context.Result = new BadRequestObjectResult(context.ModelState);
            }
        }
        public void OnActionExecuted(ActionExecutedContext context) { }

        // Bug FluentValidation cannot set values if skipped as validated
        private static void SetUnvalidatedAsValidated(ModelStateDictionary modelState)
        {
            if (modelState.ValidationState != ModelValidationState.Unvalidated)
            {
                return;
            }

            foreach (var e in modelState)
            {
                if (modelState[e.Key].ValidationState == ModelValidationState.Unvalidated)
                {
                    modelState[e.Key].ValidationState = ModelValidationState.Valid;
                }
            }
        }
    }
}
