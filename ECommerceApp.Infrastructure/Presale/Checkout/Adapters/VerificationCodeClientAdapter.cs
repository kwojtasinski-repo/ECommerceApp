using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Supporting.Verification.Services;
using ECommerceApp.Domain.Supporting.Verification;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Adapters
{
    internal sealed class VerificationCodeClientAdapter : IVerificationCodeClient
    {
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IOrderService _orderService;

        public VerificationCodeClientAdapter(
            IVerificationCodeService verificationCodeService,
            IOrderService orderService)
        {
            _verificationCodeService = verificationCodeService;
            _orderService = orderService;
        }

        public Task<string> RequestOrderAccessRecoveryAsync(int orderId, CancellationToken ct = default)
            => _verificationCodeService.GenerateAsync(
                VerificationPurpose.GuestOrderAccess,
                orderId.ToString(),
                TimeSpan.FromDays(1),
                ct);

        public async Task<OrderAccessRedemptionResult> RedeemOrderAccessRecoveryAsync(
            string code,
            CancellationToken ct = default)
        {
            var subjectKey = await _verificationCodeService.TryConsumeAsync(
                code,
                VerificationPurpose.GuestOrderAccess,
                ct);
            if (!int.TryParse(subjectKey, out var orderId) || orderId <= 0)
                return OrderAccessRedemptionResult.Failed();

            var order = await _orderService.GetOrderDetailsAsync(orderId, ct);
            return order is null
                ? OrderAccessRedemptionResult.Failed()
                : OrderAccessRedemptionResult.Succeeded(order.Id, order.CustomerId);
        }
    }
}
