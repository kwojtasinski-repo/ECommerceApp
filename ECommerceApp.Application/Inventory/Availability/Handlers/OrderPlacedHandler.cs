using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class OrderPlacedHandler : IIdAwareMessageHandler<OrderPlaced>
    {
        private readonly IStockService _stockService;
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderPlacedHandler(
            IStockService stockService,
            IInventoryUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _stockService = stockService;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(OrderPlaced message, long outboxMessageId, CancellationToken ct = default)
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
                    var dto = new ReserveStockDto(
                        item.ProductId,
                        message.OrderId,
                        item.Quantity,
                        message.UserId,
                        message.ExpiresAt);

                    await _stockService.ReserveAsync(dto, ct);
                }

                await transaction.CommitAsync(ct);
            }
        }
    }
}
