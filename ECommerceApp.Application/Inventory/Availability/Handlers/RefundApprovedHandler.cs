using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class RefundApprovedHandler : IIdAwareMessageHandler<RefundApproved>
    {
        private readonly IStockService _stockService;
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public RefundApprovedHandler(
            IStockService stockService,
            IInventoryUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _stockService = stockService;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(RefundApproved message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(RefundApproved message, long outboxMessageId, CancellationToken ct = default)
        {
            var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            await using (transaction)
            {
                var handlerType = GetType().FullName
                    ?? throw new InvalidOperationException("Handler type name is unavailable.");
                if (!await _processedMessageGuard.TryMarkProcessedAsync(
                        outboxMessageId, handlerType, transaction, ct))
                {
                    return;
                }

                foreach (var item in message.Items)
                {
                    await _stockService.ReturnAsync(item.ProductId, item.Quantity, ct);
                }

                await transaction.CommitAsync(ct);
            }
        }
    }
}
