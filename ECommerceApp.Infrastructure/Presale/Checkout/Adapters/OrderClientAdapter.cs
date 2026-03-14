using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Adapters
{
    internal sealed class OrderClientAdapter : IOrderClient
    {
        private readonly IOrderService _orderService;

        public OrderClientAdapter(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public async Task<OrderPlacementResult> PlaceOrderAsync(
            int customerId,
            int currencyId,
            string userId,
            CheckoutCustomer customer,
            IReadOnlyList<CheckoutLine> lines,
            CancellationToken ct = default)
        {
            var dto = new PlaceOrderFromPresaleDto(
                customerId,
                currencyId,
                userId,
                new OrderCustomerData(
                    customer.FirstName,
                    customer.LastName,
                    customer.Email,
                    customer.PhoneNumber,
                    customer.IsCompany,
                    customer.CompanyName,
                    customer.Nip,
                    customer.Street,
                    customer.BuildingNumber,
                    customer.FlatNumber,
                    customer.ZipCode,
                    customer.City,
                    customer.Country),
                lines.Select(l => new PlaceOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());

            var result = await _orderService.PlaceOrderFromPresaleAsync(dto, ct);

            return result.IsSuccess
                ? OrderPlacementResult.Succeeded(result.OrderId!.Value)
                : OrderPlacementResult.Failed(result.FailureReason!);
        }
    }
}
