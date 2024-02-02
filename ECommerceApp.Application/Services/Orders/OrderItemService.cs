using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Services.Orders
{
    public class OrderItemService : IOrderItemService
    {
        private readonly IMapper _mapper;
        private readonly IOrderItemRepository _orderItemRepository;

        public OrderItemService(IOrderItemRepository orderItemRepository, IMapper mapper)
        {
            _mapper = mapper;
            _orderItemRepository = orderItemRepository;
        }

        public int AddOrderItem(OrderItemDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderItemDto).Name} cannot be null");
            }

            var orderItem = _mapper.Map<OrderItem>(model);
            var orderItemExist = _orderItemRepository.GetUserOrderItemNotOrdered(model.UserId, model.ItemId);
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _orderItemRepository.UpdateOrderItem(orderItemExist);
            }
            else if (model.Id == 0)
            {
                id = _orderItemRepository.AddOrderItem(orderItem);
            }
            else
            {
                throw new BusinessException($"Check if your position with id '{model.Id}' with item with id '{model.ItemId}' is in cart", "positionNotFoundInCart", new Dictionary<string, string> { { "id", $"{model.Id}" }, { "itemId", $"{model.ItemId}" });
            }
            return id;
        }

        public void DeleteOrderItem(int id)
        {
            _orderItemRepository.DeleteOrderItem(id);
        }

        public OrderItemDto GetOrderItemDetails(int id)
        {
            return _mapper.Map<OrderItemDto>(_orderItemRepository.GetOrderItemDetailsById(id));
        }

        public IEnumerable<OrderItemDto> GetOrderItems()
        {
            var orderItems = _orderItemRepository.GetAllOrderItems();
            var orderItemsToShow = _mapper.Map<List<OrderItemDto>>(orderItems);
            return orderItemsToShow;
        }

        public IEnumerable<OrderItemDto> GetOrderItemsForRealization(string userId)
        {
            return _mapper.Map<List<OrderItemDto>>(_orderItemRepository.GetUserOrderItemsNotOrdered(userId));
        }

        public ListForOrderItemVm GetOrderItems(int pageSize, int pageNo, string searchString)
        {
            var itemOrders = _mapper.Map<List<OrderItemForListVm>>(_orderItemRepository.GetOrderItems(searchString, pageSize, pageNo));

            var itemOrderList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemOrders = itemOrders,
                Count = _orderItemRepository.GetCountBySearchString(searchString)
            };

            return itemOrderList;
        }

        public bool OrderItemExists(int id)
        {
            return _orderItemRepository.ExistsById(id);
        }

        public void UpdateOrderItem(OrderItemDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderItemDto).Name} cannot be null");
            }

            var orderItem = _orderItemRepository.GetOrderItemById(model.Id) ?? throw new BusinessException($"OrderItem with id '{model.Id}' was not found", "positionInCartNotFound", new Dictionary<string, string> { { "id", $"{model.Id}" } });
            orderItem.ItemOrderQuantity = model.ItemOrderQuantity;
            if (model.OrderId.HasValue)
            {
                orderItem.OrderId = model.OrderId.Value;
            }
            _orderItemRepository.UpdateOrderItem(orderItem);
        }

        public void UpdateOrderItems(IEnumerable<OrderItemDto> orderItemsVm)
        {
            if (!orderItemsVm.Any())
            {
                return;
            }

            var orderItems = _mapper.Map<List<OrderItem>>(orderItemsVm);
            _orderItemRepository.UpdateRange(orderItems);
        }

        public int OrderItemCount(string userId)
        {
            return _orderItemRepository.GetCountNotOrderedByUserId(userId);
        }

        public int AddOrderItem(int itemId, string userId)
        {
            var orderItemExist = _orderItemRepository.GetUserOrderItemNotOrdered(userId, itemId);
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _orderItemRepository.UpdateOrderItem(orderItemExist);
            }
            else
            {
                var orderItem = CreateOrderItem(itemId, userId);
                id = _orderItemRepository.AddOrderItem(orderItem);
            }
            return id;
        }

        private static OrderItem CreateOrderItem(int itemId, string userId)
        {
            var orderItem = new OrderItem()
            {
                Id = 0,
                ItemId = itemId,
                ItemOrderQuantity = 1,
                UserId = userId,
            };
            return orderItem;
        }

        public ListForOrderItemVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo)
        {
            var itemOrders = _mapper.Map<List<OrderItemForListVm>>(_orderItemRepository.GetOrderItemsByItemId(id, pageSize, pageNo));

            var itemOrderList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrders,
                Count = _orderItemRepository.GetCountByItemId(id)
            };

            return itemOrderList;
        }

        public ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo)
        {
            var itemOrders = _mapper.Map<List<OrderItemForListVm>>(_orderItemRepository.GetUserOrderItemsNotOrdered(userId, pageSize, pageNo));

            var ordersList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrders,
                Count = _orderItemRepository.GetCountNotOrderedByUserId(userId)
            };

            return ordersList;
        }

        public IEnumerable<OrderItemDto> GetOrderItemsByItemId(int itemId)
        {
            return _mapper.Map<List<OrderItemDto>>(_orderItemRepository.GetOrderItemsByItemId(itemId));
        }

        public List<OrderItemDto> GetOrderItemsNotOrdered(IEnumerable<int> ids)
        {
            return _mapper.Map<List<OrderItemDto>>(_orderItemRepository.GetOrderItemsToRealization(ids));
        }

        public IEnumerable<int> GetOrderItemsIdsForRealization(string userId)
        {
            return _orderItemRepository.GetUserOrderItemsId(userId);
        }
    }
}
