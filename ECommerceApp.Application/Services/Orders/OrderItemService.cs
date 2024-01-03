using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Orders
{
    public class OrderItemService : AbstractService<OrderItemVm, IOrderItemRepository, OrderItem>, IOrderItemService
    {
        public OrderItemService(IOrderItemRepository orderItemRepository, IMapper mapper) : base(orderItemRepository, mapper)
        {
        }

        public int AddOrderItem(OrderItemDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderItemDto).Name} cannot be null");
            }

            var orderItem = _mapper.Map<OrderItem>(model);
            var orderItemExist = _repo.GetAllOrderItems()
                                      .FirstOrDefault(oi => oi.ItemId == model.ItemId && oi.UserId == model.UserId && oi.OrderId == null);
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _repo.UpdateOrderItem(orderItemExist);
            }
            else if (model.Id == 0)
            {
                id = _repo.AddOrderItem(orderItem);
            }
            else
            {
                throw new BusinessException("Given invalid orderItem");
            }
            return id;
        }

        public void DeleteOrderItem(int id)
        {
            _repo.DeleteOrderItem(id);
        }

        public OrderItemDto GetOrderItemDetails(int id)
        {
            var orderItem = _repo.GetAll().Include(i => i.Item).Where(oi => oi.Id == id).AsNoTracking().FirstOrDefault();
            var orderItemVm = _mapper.Map<OrderItemDto>(orderItem);
            return orderItemVm;
        }

        public IEnumerable<OrderItemDto> GetOrderItems(Expression<Func<OrderItem, bool>> expression)
        {
            var orderItems = _repo.GetAll().Include(i => i.Item).Where(expression).AsNoTracking().ToList();
            var orderItemsToShow = _mapper.Map<List<OrderItemDto>>(orderItems);
            return orderItemsToShow;
        }

        public IEnumerable<OrderItemDto> GetOrderItemsForRealization(string userId)
        {
            var orderItems = _repo.GetAll().Include(i => i.Item).Where(oi => oi.UserId == userId && oi.OrderId == null).AsNoTracking().ToList();
            var orderItemsToShow = _mapper.Map<List<OrderItemDto>>(orderItems);
            return orderItemsToShow;
        }

        public ListForOrderItemVm GetOrderItems(int pageSize, int pageNo, string searchString)
        {
            var itemOrder = _repo.GetAllOrderItems().Where(oi => oi.Item.Name.StartsWith(searchString) ||
                            oi.Item.Brand.Name.StartsWith(searchString) || oi.Item.Type.Name.StartsWith(searchString))
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
        }

        public bool OrderItemExists(int id)
        {
            return _repo.GetAll().AsNoTracking().Any(oi => oi.Id == id);
        }

        public void UpdateOrderItem(OrderItemDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderItemDto).Name} cannot be null");
            }

            var orderItem = _repo.GetById(model.Id) ?? throw new BusinessException($"OrderItem with id '{model.Id}' was not found");
            orderItem.ItemOrderQuantity = model.ItemOrderQuantity;
            if (model.OrderId.HasValue)
            {
                orderItem.OrderId = model.OrderId.Value;
            }
            _repo.UpdateOrderItem(orderItem);
        }

        public void UpdateOrderItems(IEnumerable<OrderItemDto> orderItemsVm)
        {
            if (!orderItemsVm.Any())
            {
                return;
            }

            var orderItems = _mapper.Map<List<OrderItem>>(orderItemsVm);
            _repo.UpdateRange(orderItems);
        }

        public int OrderItemCount(string userId)
        {
            return _repo.GetAllOrderItems()
                        .Where(oi => oi.UserId == userId && oi.OrderId == null)
                        .Count();
        }

        public int AddOrderItem(int itemId, string userId)
        {
            var orderItemExist = _repo.GetAll().Where(oi => oi.ItemId == itemId && oi.UserId == userId && oi.OrderId == null).FirstOrDefault();
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _repo.UpdateOrderItem(orderItemExist);
            }
            else
            {
                var orderItem = CreateOrderItem(itemId, userId);
                id = _repo.AddOrderItem(orderItem);
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
            var itemOrder = _repo.GetAllOrderItems().Where(oi => oi.ItemId == id)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
        }

        public ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo)
        {
            var itemOrders = _repo.GetAllOrderItems().Include(i => i.Item).Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider);

            var itemOrdersToShow = itemOrders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrdersToShow,
                Count = itemOrders.Count()
            };

            return ordersList;
        }

        public IQueryable<OrderItem> GetOrderItems()
        {
            var orderItems = _repo.GetAll();
            return orderItems;
        }

        public IEnumerable<OrderItemDto> GetOrderItemsByItemId(int itemId)
        {
            var orderItems = _repo.GetAll()
                                  .Where(oi => oi.ItemId == itemId)
                                  .AsNoTracking()
                                  .ToList();
            return _mapper.Map<List<OrderItemDto>>(orderItems);
        }
    }
}
