using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Sales.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Services
{
    internal sealed class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IOrderItemRepository _orderItemRepo;
        private readonly ICustomerExistenceChecker _customerChecker;
        private readonly IMessageBroker _messageBroker;

        public OrderService(
            IOrderRepository orderRepo,
            IOrderItemRepository orderItemRepo,
            ICustomerExistenceChecker customerChecker,
            IMessageBroker messageBroker)
        {
            _orderRepo = orderRepo;
            _orderItemRepo = orderItemRepo;
            _customerChecker = customerChecker;
            _messageBroker = messageBroker;
        }

        public async Task<PlaceOrderResult> PlaceOrderAsync(PlaceOrderDto dto, CancellationToken ct = default)
        {
            var customerExists = await _customerChecker.ExistsAsync(dto.CustomerId, ct);
            if (!customerExists)
                return PlaceOrderResult.CustomerNotFound(dto.CustomerId);

            var cartItems = await _orderItemRepo.GetByIdsAsync(dto.CartItemIds, ct);
            if (cartItems.Count == 0)
                return PlaceOrderResult.CartItemsNotFound();

            if (cartItems.Any(i => i.UserId != dto.UserId))
                return PlaceOrderResult.CartItemsNotOwnedByUser();

            var cost = cartItems.Sum(i => i.UnitCost * i.Quantity);
            var number = Guid.NewGuid().ToString();
            var order = Order.Create(dto.CustomerId, dto.CurrencyId, dto.UserId, number, cost);
            var orderId = await _orderRepo.AddAsync(order, ct);

            await _orderItemRepo.AssignToOrderAsync(dto.CartItemIds, orderId, ct);

            var items = cartItems
                .Select(i => new OrderPlacedItem(i.ItemId, i.Quantity))
                .ToList();

            await _messageBroker.PublishAsync(new OrderPlaced(
                orderId,
                items,
                dto.UserId,
                DateTime.UtcNow.AddDays(3),
                DateTime.UtcNow));

            return PlaceOrderResult.Success(orderId);
        }

        public async Task<OrderDetailsVm?> GetOrderDetailsAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            return order is null ? null : MapToDetailsVm(order);
        }

        public async Task<OrderOperationResult> UpdateOrderAsync(UpdateOrderDto dto, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(dto.OrderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;

            order.Update(dto.CustomerId, dto.CurrencyId);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> DeleteOrderAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;

            await _orderRepo.DeleteAsync(orderId, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> MarkAsDeliveredAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;
            if (!order.IsPaid)
                return OrderOperationResult.NotPaid;
            if (order.IsDelivered)
                return OrderOperationResult.AlreadyDelivered;

            var @event = order.MarkAsDelivered();
            await _orderRepo.UpdateAsync(order, ct);

            var items = order.OrderItems
                .Select(i => new OrderShippedItem(i.ItemId, i.Quantity))
                .ToList();

            await _messageBroker.PublishAsync(new OrderShipped(orderId, items, @event.OccurredAt));
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> AddCouponAsync(int orderId, int couponUsedId, int discountPercent, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;

            order.AssignCoupon(couponUsedId, discountPercent);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> RemoveCouponAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;
            if (order.CouponUsedId is null)
                return OrderOperationResult.CouponNotAssigned;

            order.RemoveCoupon();
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> AddRefundAsync(int orderId, int refundId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;

            order.AssignRefund(refundId);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> RemoveRefundByRefundIdAsync(int refundId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByRefundIdWithItemsAsync(refundId);
            if (order is null)
                return OrderOperationResult.OrderNotFound;

            order.RemoveRefund();
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderListVm> GetAllOrdersAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var orders = await _orderRepo.GetAllAsync(pageSize, pageNo, search, ct);
            var count = await _orderRepo.GetAllCountAsync(search, ct);
            return new OrderListVm
            {
                Orders = orders.Select(MapToForListVm).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = count,
                SearchString = search
            };
        }

        public async Task<IReadOnlyList<OrderForListVm>> GetOrdersByUserIdAsync(string userId, CancellationToken ct = default)
        {
            var orders = await _orderRepo.GetByUserIdAsync(userId, ct);
            return orders.Select(MapToForListVm).ToList();
        }

        public async Task<IReadOnlyList<OrderForListVm>> GetOrdersByCustomerIdAsync(int customerId, CancellationToken ct = default)
        {
            var orders = await _orderRepo.GetByCustomerIdAsync(customerId, ct);
            return orders.Select(MapToForListVm).ToList();
        }

        public async Task<OrderListVm> GetAllPaidOrdersAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var orders = await _orderRepo.GetAllPaidAsync(pageSize, pageNo, search, ct);
            var count = await _orderRepo.GetAllPaidCountAsync(search, ct);
            return new OrderListVm
            {
                Orders = orders.Select(MapToForListVm).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = count,
                SearchString = search
            };
        }

        public Task<int?> GetCustomerIdAsync(int orderId, CancellationToken ct = default)
            => _orderRepo.GetCustomerIdAsync(orderId, ct);

        private static OrderDetailsVm MapToDetailsVm(Domain.Sales.Orders.Order order)
            => new()
            {
                Id = order.Id.Value,
                Number = order.Number,
                Cost = order.Cost,
                Ordered = order.Ordered,
                Delivered = order.Delivered,
                IsDelivered = order.IsDelivered,
                IsPaid = order.IsPaid,
                CustomerId = order.CustomerId,
                CurrencyId = order.CurrencyId,
                UserId = order.UserId,
                PaymentId = order.PaymentId,
                RefundId = order.RefundId,
                CouponUsedId = order.CouponUsedId,
                DiscountPercent = order.DiscountPercent,
                OrderItems = order.OrderItems.Select(i => new OrderItemVm
                {
                    Id = i.Id.Value,
                    ItemId = i.ItemId,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    CouponUsedId = i.CouponUsedId,
                    RefundId = i.RefundId
                }).ToList()
            };

        private static OrderForListVm MapToForListVm(Domain.Sales.Orders.Order order)
            => new()
            {
                Id = order.Id.Value,
                Number = order.Number,
                Cost = order.Cost,
                Ordered = order.Ordered,
                IsDelivered = order.IsDelivered,
                IsPaid = order.IsPaid,
                CustomerId = order.CustomerId,
                CurrencyId = order.CurrencyId
            };
    }
}
