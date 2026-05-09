using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Contracts;
using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.ViewModels;
using ECommerceApp.Domain.Sales.Fulfillment;

namespace ECommerceApp.Application.Sales.Fulfillment.Services
{
    internal sealed class RefundService : IRefundService
    {
        private readonly IRefundRepository _refunds;
        private readonly IOrderExistenceChecker _orderExistence;
        private readonly IMessageBroker _broker;

        public RefundService(
            IRefundRepository refunds,
            IOrderExistenceChecker orderExistence,
            IMessageBroker broker)
        {
            _refunds = refunds;
            _orderExistence = orderExistence;
            _broker = broker;
        }

        public async Task<RefundRequestResult> RequestRefundAsync(RequestRefundDto dto, CancellationToken ct = default)
        {
            if (!await _orderExistence.ExistsAsync(dto.OrderId, ct))
            {
                return RefundRequestResult.OrderNotFound;
            }

            var existing = await _refunds.FindActiveByOrderIdAsync(dto.OrderId, ct);
            if (existing is not null)
            {
                return RefundRequestResult.RefundAlreadyExists;
            }

            var items = dto.Items.Select(i => RefundItem.Create(i.ProductId, i.Quantity));
            var refund = Refund.Create(dto.OrderId, dto.Reason, dto.OnWarranty, items, dto.UserId);
            await _refunds.AddAsync(refund, ct);

            return RefundRequestResult.Requested;
        }

        public async Task<RefundOperationResult> ApproveRefundAsync(int refundId, CancellationToken ct = default)
        {
            var refund = await _refunds.GetByIdAsync(refundId, ct);
            if (refund is null)
            {
                return RefundOperationResult.RefundNotFound;
            }

            if (refund.Status != RefundStatus.Requested)
            {
                return RefundOperationResult.AlreadyProcessed;
            }

            refund.Approve();
            await _refunds.UpdateAsync(refund, ct);

            var approvedItems = refund.Items
                .Select(i => new RefundApprovedItem(i.ProductId, i.Quantity))
                .ToList();

            await _broker.PublishAsync(new RefundApproved(
                refund.Id.Value,
                refund.OrderId,
                approvedItems,
                DateTime.UtcNow));

            return RefundOperationResult.Success;
        }

        public async Task<RefundOperationResult> RejectRefundAsync(int refundId, CancellationToken ct = default)
        {
            var refund = await _refunds.GetByIdAsync(refundId, ct);
            if (refund is null)
            {
                return RefundOperationResult.RefundNotFound;
            }

            if (refund.Status != RefundStatus.Requested)
            {
                return RefundOperationResult.AlreadyProcessed;
            }

            refund.Reject();
            await _refunds.UpdateAsync(refund, ct);

            await _broker.PublishAsync(new RefundRejected(
                refund.Id.Value,
                refund.OrderId,
                DateTime.UtcNow));

            return RefundOperationResult.Success;
        }

        public async Task<RefundDetailsVm> GetRefundAsync(int refundId, CancellationToken ct = default)
        {
            var refund = await _refunds.GetByIdAsync(refundId, ct);
            if (refund is null)
                return null;

            return MapToDetailsVm(refund);
        }

        public async Task<RefundListVm> GetRefundsAsync(int pageSize, int pageNo, string search, CancellationToken ct = default)
        {
            var refunds = await _refunds.GetPagedAsync(pageSize, pageNo, search, ct);
            var count = await _refunds.GetCountAsync(search, ct);

            return new RefundListVm
            {
                Refunds = refunds.Select(MapToVm).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = count,
                SearchString = search
            };
        }

        public async Task<IReadOnlyList<RefundVm>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
        {
            var refunds = await _refunds.GetByOrderIdAsync(orderId, ct);
            return refunds.Select(MapToVm).ToList();
        }

        private static RefundVm MapToVm(Refund refund)
            => new(
                refund.Id.Value,
                refund.OrderId,
                refund.Reason,
                refund.OnWarranty,
                refund.Status.ToString(),
                refund.RequestedAt,
                refund.ProcessedAt,
                refund.UserId);

        private static RefundDetailsVm MapToDetailsVm(Refund refund)
            => new(
                refund.Id.Value,
                refund.OrderId,
                refund.Reason,
                refund.OnWarranty,
                refund.Status.ToString(),
                refund.RequestedAt,
                refund.ProcessedAt,
                refund.Items.Select(i => new RefundItemVm(i.ProductId, i.Quantity)).ToList(),
                refund.UserId);
    }
}
