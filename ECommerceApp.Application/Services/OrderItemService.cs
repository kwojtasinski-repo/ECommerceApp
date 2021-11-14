using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class OrderItemService : AbstractService<OrderItemVm, IOrderItemRepository, OrderItem>, IOrderItemService
    {
        public OrderItemService(IOrderItemRepository orderItemRepository, IMapper mapper) : base(orderItemRepository, mapper)
        {
        }

        public int AddOrderItem(OrderItemVm model)
        {
            var orderItem = _mapper.Map<OrderItem>(model);
            var orderItemExist = _repo.GetOrderItemById(model.Id);
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _repo.UpdateOrderItem(orderItemExist);
            }
            else
            {
                id = _repo.AddOrderItem(orderItem);
            }
            return id;
        }

        public void DeleteOrderItem(int id)
        {
            _repo.DeleteOrderItem(id);
        }

        public OrderItemVm GetOrderItemById(int id)
        {
            var orderItem = Get(id);
            return orderItem;
        }

        public OrderItemDetailsVm GetOrderItemDetails(int id)
        {
            var orderItem = _repo.GetAll().Include(i => i.Item).Where(oi => oi.Id == id).FirstOrDefault();
            if (orderItem != null)
            {
                _repo.DetachEntity(orderItem);
            }
            var orderItemVm = _mapper.Map<OrderItemDetailsVm>(orderItem);
            return orderItemVm;
        }

        public IEnumerable<OrderItemVm> GetOrderItems(Expression<Func<OrderItem, bool>> expression)
        {
            var orderItems = _repo.GetAll().Where(expression).ToList();
            if (orderItems.Count > 0)
            {
                _repo.DetachEntity(orderItems);
            }
            var orderItemsToShow = _mapper.Map<List<OrderItemVm>>(orderItems);
            return orderItemsToShow;
        }

        public IEnumerable<NewOrderItemVm> GetOrderItemsForRealization(Expression<Func<OrderItem, bool>> expression)
        {
            var orderItems = _repo.GetAll().Include(i => i.Item).Where(expression).ToList();
            if (orderItems.Count > 0)
            {
                _repo.DetachEntity(orderItems);
            }
            var orderItemsToShow = _mapper.Map<List<NewOrderItemVm>>(orderItems);
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
            var orderItem = _repo.GetAll().Where(oi => oi.Id == id).FirstOrDefault();
            var exists = orderItem != null;

            if (exists)
            {
                _repo.DetachEntity(orderItem);
            }

            return exists;
        }

        public void UpdateOrderItem(OrderItemVm model)
        {
            var orderItem = _mapper.Map<OrderItem>(model);
            _repo.UpdateOrderItem(orderItem);
        }

        public void UpdateOrderItems(IEnumerable<OrderItemVm> orderItemsVm)
        {
            if (orderItemsVm.Count() == 0)
            {
                return;
            }

            var orderItems = _mapper.Map<List<OrderItem>>(orderItemsVm);
            _repo.UpdateRange(orderItems);
        }

        public int OrderItemCount(string userId)
        {
            var itemOrders = _repo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return itemOrders.Count;
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

        private OrderItem CreateOrderItem(int itemId, string userId)
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
    }
}
