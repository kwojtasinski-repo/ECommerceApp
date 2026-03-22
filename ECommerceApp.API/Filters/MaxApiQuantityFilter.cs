using ECommerceApp.API.Options;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECommerceApp.API.Filters
{
    public sealed class MaxApiQuantityFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("dto", out var arg) && arg is AddToCartDto dto)
            {
                if (dto.Quantity > ApiPurchaseOptions.MaxQuantityPerOrderLine)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        Error = $"Quantity cannot exceed {ApiPurchaseOptions.MaxQuantityPerOrderLine} units of a single product per order."
                    });
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
