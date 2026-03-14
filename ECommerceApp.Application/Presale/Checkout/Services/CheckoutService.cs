using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Presale.Checkout;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class CheckoutService : ICheckoutService
    {
        private readonly ISoftReservationService _softReservationService;
        private readonly IStockClient _stockClient;
        private readonly IOrderService _orderService;
        private readonly ICartService _cartService;

        public CheckoutService(
            ISoftReservationService softReservationService,
            IStockClient stockClient,
            IOrderService orderService,
            ICartService cartService)
        {
            _softReservationService = softReservationService;
            _stockClient = stockClient;
            _orderService = orderService;
            _cartService = cartService;
        }

        public Task<CheckoutResult> PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId, CancellationToken ct = default)
        {
            throw new NotImplementedException("Blocked: awaiting Orders BC atomic switch — see project-state.md and presale-slice2.md §14");
        }
    }
}
