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
        private readonly IItemRepository _itemRepository;

        public OrderItemService(IOrderItemRepository orderItemRepository, IMapper mapper, IItemRepository itemRepository)
        {
            _mapper = mapper;
            _orderItemRepository = orderItemRepository;
            _itemRepository = itemRepository;
        }

        public int AddOrderItem(OrderItemDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderItemDto).Name} cannot be null");
            }

            var item = _itemRepository.GetItemById(model.ItemId)
                ?? throw new BusinessException($"Item with id '{model.ItemId}' was not found", "itemNotFound", new Dictionary<string, string> { { "id", $"{model.ItemId}"} });
            if (item.Quantity <= 0)
            {
                throw new BusinessException($"Item with id '{item.Id}' is not available", "itemNotInStock", new Dictionary<string, string> { { "id", $"{item.Id}" }, { "name", item.Name } });
            }

            if (item.Quantity < model.ItemOrderQuantity)
            {
                throw new BusinessException($"Item with id '{item.Id}' cannot be ordered with quantity of '{model.ItemOrderQuantity}', available '{item.Quantity}'", "tooManyItemsQuantityInCart", new Dictionary<string, string> { { "id", $"{item.Id}" }, { "name", item.Name }, { "availableQuantity", $"{item.Quantity}" } });
            }

            item.Quantity -= model.ItemOrderQuantity;
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
                throw new BusinessException($"Check if your position with id '{model.Id}' with item with id '{model.ItemId}' is in cart", "positionNotFoundInCart", new Dictionary<string, string> { { "id", $"{model.Id}" }, { "itemId", $"{model.ItemId}" } });
            }
            return id;
        }

        public bool DeleteOrderItem(int id)
        {
            return _orderItemRepository.DeleteOrderItem(id);
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
            // TODO: Think about return more than one error
            var orderItems = _orderItemRepository.GetUserOrderItemsNotOrdered(userId);
            foreach (var orderItem in orderItems)
            {
                if (orderItem.Item.Quantity <= 0)
                {
                    throw new BusinessException($"Item with id '{orderItem.ItemId}' is not available", "itemNotInStock", new Dictionary<string, string> { { "id", $"{orderItem.ItemId}" }, { "name", orderItem.Item.Name } });
                }

                if (orderItem.Item.Quantity < orderItem.ItemOrderQuantity)
                {
                    throw new BusinessException($"Item with id '{orderItem.ItemId}' cannot be ordered with quantity of '{orderItem.ItemOrderQuantity}', available '{orderItem.Item.Quantity}'", "tooManyItemsQuantityInCart", new Dictionary<string, string> { { "id", $"{orderItem.ItemId}" }, { "name", orderItem.Item.Name }, { "availableQuantity", $"{orderItem.Item.Quantity}" } });
                }
            }

            return _mapper.Map<List<OrderItemDto>>(orderItems);
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

        public bool UpdateOrderItem(OrderItemDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderItemDto).Name} cannot be null");
            }

            var orderItem = _orderItemRepository.GetOrderItemById(model.Id);
            if (orderItem is null)
            {
                return false;
            }

            var totalItemQuantity = model.ItemOrderQuantity - orderItem.ItemOrderQuantity;
            if (orderItem.Item.Quantity < totalItemQuantity)
            {
                throw new BusinessException($"Item with id '{orderItem.ItemId}' cannot be ordered with quantity of '{orderItem.ItemOrderQuantity}', available '{orderItem.Item.Quantity}'", "tooManyItemsQuantityInCart", new Dictionary<string, string> { { "id", $"{orderItem.ItemId}" }, { "name", orderItem.Item.Name }, { "availableQuantity", $"{orderItem.Item.Quantity}" } });
            }

            orderItem.Item.Quantity -= totalItemQuantity;
            orderItem.ItemOrderQuantity = model.ItemOrderQuantity;
            if (model.OrderId.HasValue)
            {
                orderItem.OrderId = model.OrderId.Value;
            }
            _orderItemRepository.UpdateOrderItem(orderItem);
            return true;
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
