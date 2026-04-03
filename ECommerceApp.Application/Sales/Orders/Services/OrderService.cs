using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Shared;
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
        private readonly IOrderCustomerResolver _customerResolver;
        private readonly IMessageBroker _messageBroker;

        public OrderService(
            IOrderRepository orderRepo,
            IOrderItemRepository orderItemRepo,
            ICustomerExistenceChecker customerChecker,
            IOrderCustomerResolver customerResolver,
            IMessageBroker messageBroker)
        {
            _orderRepo = orderRepo;
            _orderItemRepo = orderItemRepo;
            _customerChecker = customerChecker;
            _customerResolver = customerResolver;
            _messageBroker = messageBroker;
        }

        public async Task<PlaceOrderResult> PlaceOrderAsync(PlaceOrderDto dto, CancellationToken ct = default)
        {
            var customerExists = await _customerChecker.ExistsAsync(dto.CustomerId, ct);
            if (!customerExists)
            {
                return PlaceOrderResult.CustomerNotFound(dto.CustomerId);
            }

            var cartItems = await _orderItemRepo.GetByIdsAsync(dto.CartItemIds, ct);
            if (cartItems.Count == 0)
            {
                return PlaceOrderResult.CartItemsNotFound();
            }

            if (cartItems.Any(i => (string)i.UserId != dto.UserId))
            {
                return PlaceOrderResult.CartItemsNotOwnedByUser();
            }

            var customer = await _customerResolver.ResolveAsync(dto.CustomerId, ct);
            var number = OrderNumber.Generate();
            var order = Order.Create(dto.CustomerId, dto.CurrencyId, dto.UserId, number, customer);
            var orderId = await _orderRepo.AddAsync(order, ct);

            await _orderItemRepo.AssignToOrderAsync(dto.CartItemIds, orderId, ct);

            var orderWithItems = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            orderWithItems!.CalculateCost();
            await _orderRepo.UpdateAsync(orderWithItems, ct);

            var items = cartItems
                .Select(i => new OrderPlacedItem(i.ItemId.Value, i.Quantity))
                .ToList();

            await _messageBroker.PublishAsync(new OrderPlaced(
                orderId,
                items,
                dto.UserId,
                DateTime.UtcNow.AddDays(3),
                DateTime.UtcNow,
                orderWithItems!.Cost,
                orderWithItems.CurrencyId));

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
            {
                return OrderOperationResult.OrderNotFound;
            }

            order.Update(dto.CustomerId, dto.CurrencyId);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> DeleteOrderAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, ct);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

            await _orderRepo.DeleteAsync(orderId, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> MarkAsDeliveredAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
                return OrderOperationResult.OrderNotFound;
            if (order.Status != OrderStatus.PaymentConfirmed && order.Status != OrderStatus.PartiallyFulfilled)
                return OrderOperationResult.NotPaid;
            if (order.Status == OrderStatus.Fulfilled)
                return OrderOperationResult.AlreadyDelivered;

            order.Fulfill();
            await _orderRepo.UpdateAsync(order, ct);

            var fulfilledAt = order.Events
                .Last(e => e.EventType == OrderEventType.OrderFulfilled)
                .OccurredAt;

            var items = order.OrderItems
                .Select(i => new OrderShippedItem(i.ItemId.Value, i.Quantity))
                .ToList();

            await _messageBroker.PublishAsync(new OrderShipped(orderId, items, fulfilledAt));
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> AddCouponAsync(int orderId, int couponUsedId, int discountPercent, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

            order.AssignCoupon(couponUsedId, discountPercent);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> RemoveCouponAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

            if (order.CouponUsedId is null)
            {
                return OrderOperationResult.CouponNotAssigned;
            }

            order.RemoveCoupon();
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> AddRefundAsync(int orderId, int refundId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, ct);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

            order.AssignRefund(refundId);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> RemoveRefundByRefundIdAsync(int refundId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByRefundIdWithItemsAsync(refundId);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

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

        public async Task<OrderOperationResult> MarkAsPaidAsync(int orderId, int paymentId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(orderId, ct);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

            if (order.Status != OrderStatus.Placed)
            {
                return OrderOperationResult.AlreadyPaid;
            }

            order.ConfirmPayment(paymentId);
            await _orderRepo.UpdateAsync(order, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderOperationResult> CancelOrderAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(orderId, ct);
            if (order is null)
            {
                return OrderOperationResult.OrderNotFound;
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                return OrderOperationResult.AlreadyCancelled;
            }

            if (order.Status != OrderStatus.Placed)
            {
                return OrderOperationResult.AlreadyPaid;
            }

            var items = order.OrderItems
                .Select(i => new OrderCancelledItem(i.ItemId.Value, i.Quantity))
                .ToList();

            order.Cancel("ManualOperator");
            await _orderRepo.UpdateAsync(order, ct);

            await _messageBroker.PublishAsync(new OrderCancelled(orderId, items, DateTime.UtcNow));
            return OrderOperationResult.Success;
        }

        public async Task<PlaceOrderResult> PlaceOrderFromPresaleAsync(PlaceOrderFromPresaleDto dto, CancellationToken ct = default)
        {
            if (dto.Lines.Count == 0)
            {
                return PlaceOrderResult.CartItemsNotFound();
            }

            var customer = new OrderCustomer(
                dto.Customer.FirstName,
                dto.Customer.LastName,
                dto.Customer.Email,
                dto.Customer.PhoneNumber,
                dto.Customer.IsCompany,
                dto.Customer.CompanyName,
                dto.Customer.Nip,
                dto.Customer.Street,
                dto.Customer.BuildingNumber,
                dto.Customer.FlatNumber,
                dto.Customer.ZipCode,
                dto.Customer.City,
                dto.Customer.Country);

            var number = OrderNumber.Generate();
            var order = Order.Create(dto.CustomerId, dto.CurrencyId, dto.UserId, number, customer);

            foreach (var line in dto.Lines)
            {
                var item = OrderItem.Create(
                    new OrderProductId(line.ProductId),
                    line.Quantity,
                    new UnitCost(line.UnitPrice),
                    dto.UserId);
                order.AddItem(item);
            }

            order.CalculateCost();
            var orderId = await _orderRepo.AddAsync(order, ct);

            var items = dto.Lines
                .Select(l => new OrderPlacedItem(l.ProductId, l.Quantity))
                .ToList();

            await _messageBroker.PublishAsync(new OrderPlaced(
                orderId,
                items,
                dto.UserId,
                DateTime.UtcNow.AddDays(3),
                DateTime.UtcNow,
                order.Cost,
                dto.CurrencyId));

            return PlaceOrderResult.Success(orderId);
        }

        private static OrderDetailsVm MapToDetailsVm(Order order)
            => new()
            {
                Id = order.Id.Value,
                Number = order.Number.Value,
                Cost = order.Cost,
                Ordered = order.Ordered,
                Status = order.Status,
                CustomerId = order.CustomerId,
                CurrencyId = order.CurrencyId,
                UserId = order.UserId,
                CouponUsedId = order.CouponUsedId,
                DiscountPercent = order.DiscountPercent,
                Customer = order.Customer is null ? null : new OrderCustomerVm
                {
                    FirstName = order.Customer.FirstName,
                    LastName = order.Customer.LastName,
                    Email = order.Customer.Email,
                    PhoneNumber = order.Customer.PhoneNumber,
                    IsCompany = order.Customer.IsCompany,
                    CompanyName = order.Customer.CompanyName,
                    Nip = order.Customer.Nip,
                    Street = order.Customer.Street,
                    BuildingNumber = order.Customer.BuildingNumber,
                    FlatNumber = order.Customer.FlatNumber,
                    ZipCode = order.Customer.ZipCode,
                    City = order.Customer.City,
                    Country = order.Customer.Country
                },
                OrderItems = order.OrderItems.Select(i => new OrderItemVm
                {
                    Id = i.Id.Value,
                    ItemId = i.ItemId,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost.Amount,
                    CouponUsedId = i.CouponUsedId,
                    ProductName = i.Snapshot?.ProductName,
                    ImageFileName = i.Snapshot?.ImageFileName
                }).ToList(),
                Events = order.Events
                    .OrderBy(e => e.OccurredAt)
                    .Select(e => new OrderEventVm
                    {
                        EventType = GetEventLabel(e.EventType),
                        OccurredAt = e.OccurredAt
                    }).ToList()
            };

        private static string GetEventLabel(OrderEventType eventType) => eventType switch
        {
            OrderEventType.OrderPlaced => "Zamówienie złożone",
            OrderEventType.OrderPaymentConfirmed => "Płatność potwierdzona",
            OrderEventType.OrderPaymentExpired => "Płatność wygasła",
            OrderEventType.OrderFulfilled => "Zamówienie zrealizowane",
            OrderEventType.CouponApplied => "Kupon zastosowany",
            OrderEventType.CouponRemoved => "Kupon usunięty",
            OrderEventType.RefundAssigned => "Zwrot przypisany",
            OrderEventType.RefundRemoved => "Zwrot usunięty",
            OrderEventType.OrderCancelled => "Zamówienie anulowane",
            OrderEventType.PriceAdjusted => "Cena dostosowana",
            OrderEventType.ShipmentDispatched => "Przesyłka wysłana",
            OrderEventType.ShipmentFailed => "Przesyłka nieudana",
            OrderEventType.PartiallyFulfilled => "Częściowo zrealizowane",
            _ => eventType.ToString()
        };

        private static OrderForListVm MapToForListVm(Order order)
            => new()
            {
                Id = order.Id.Value,
                Number = order.Number.Value,
                Cost = order.Cost,
                Ordered = order.Ordered,
                Status = order.Status,
                CustomerId = order.CustomerId,
                CurrencyId = order.CurrencyId
            };
    }
}
