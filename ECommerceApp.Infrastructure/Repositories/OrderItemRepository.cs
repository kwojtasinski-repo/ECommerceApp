using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class OrderItemRepository : IOrderItemRepository
    {
        private readonly Context _context;
        private readonly IGenericRepository<OrderItem> _repository;

        public OrderItemRepository(Context context, IGenericRepository<OrderItem> repository)
        {
            _context = context;
            _repository = repository;
        }

        public int AddOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Add(orderItem);
            _context.SaveChanges();
            return orderItem.Id;
        }

        public bool DeleteOrderItem(int orderItemId)
        {
            var orderItem = _context.OrderItem.Find(orderItemId);
            if (orderItem is null)
            {
                return false;
            }

            _context.OrderItem.Remove(orderItem);
            return _context.SaveChanges() > 0;
        }

        public List<OrderItem> GetAllOrderItems()
        {
            return _context.OrderItem
                           .Include(i => i.Item)
                           .ToList();
        }

        public List<OrderItem> GetOrderItemsByItemId(int itemId)
        {
            return _context.OrderItem
                           .Where(oi => oi.ItemId == itemId)
                           .ToList();
        }

        public OrderItem GetOrderItemById(int orderItemId)
        {
            var orderItem = _context.OrderItem.FirstOrDefault(o => o.Id == orderItemId);
            return orderItem;
        }

        public List<OrderItem> GetOrderItemsToRealization(IEnumerable<int> ids)
        {
            return _context.OrderItem
                .Where(oi => ids.Contains(oi.Id) && oi.OrderId == null)
                .Include(i => i.Item)
                .ToList()
                ?? new List<OrderItem>();
        }

        public List<int> GetUserOrderItemsId(string userId)
        {
            return _context.OrderItem
                           .Where(oi => oi.UserId == userId && oi.OrderId == null)
                           .AsNoTracking()
                           .Select(oi => oi.Id)
                           .ToList();
        }

        public void UpdateOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Update(orderItem);
            _context.SaveChanges();
        }

        public void UpdateRange(List<OrderItem> orderItems)
        {
            _repository.UpdateRange(orderItems);
        }

        public List<OrderItem> GetUserOrderItemsNotOrdered(string userId, int pageSize, int pageNo)
        {
            return _repository.GetAll()
                              .Include(i => i.Item).Where(oi => oi.UserId == userId && oi.OrderId == null)
                              .Skip(pageSize * (pageNo - 1))
                              .Take(pageSize)
                              .ToList();
        }

        public int GetCountNotOrderedByUserId(string userId)
        {
            return _repository.GetAll()
                              .Include(i => i.Item).Where(oi => oi.UserId == userId && oi.OrderId == null)
                              .Count();
        }

        public List<OrderItem> GetOrderItemsByItemId(int itemId, int pageSize, int pageNo)
        {
            return _repository.GetAll()
                              .Where(oi => oi.ItemId == itemId)
                              .Skip(pageSize * (pageNo - 1))
                              .Take(pageSize)
                              .ToList();
        }

        public int GetCountByItemId(int itemId)
        {
            return _repository.GetAll()
                              .Where(oi => oi.ItemId == itemId)
                              .Count();
        }

        public OrderItem GetUserOrderItemNotOrdered(string userId, int itemId)
        {
            return _repository.GetAll()
                              .FirstOrDefault(oi => oi.ItemId == itemId && oi.UserId == userId && oi.OrderId == null);
        }

        public bool ExistsById(int id)
        {
            return _context.OrderItem.AsNoTracking().Any(oi => oi.Id == id);
        }

        public List<OrderItem> GetOrderItems(string searchString, int pageSize, int pageNo)
        {
            return _context.OrderItem
                           .Where(oi => oi.Item.Name.StartsWith(searchString) ||
                                oi.Item.Brand.Name.StartsWith(searchString) || oi.Item.Type.Name.StartsWith(searchString))
                           .Include(oi => oi.Item).ThenInclude(i => i.Brand)
                           .Include(oi => oi.Item).ThenInclude(i => i.Type)
                           .Select(oi => new OrderItem
                           {
                               Id = oi.Id,
                               UserId = oi.UserId,
                               OrderId = oi.OrderId,
                               CouponUsedId = oi.CouponUsedId,
                               ItemId = oi.ItemId,
                               ItemOrderQuantity = oi.ItemOrderQuantity,
                               RefundId = oi.RefundId,
                               Item = new Item
                               { 
                                   Id = oi.ItemId,
                                   Name = oi.Item.Name,
                                   Cost = oi.Item.Cost,
                                   Brand = oi.Item.Brand,
                                   Type = oi.Item.Type,
                               }
                           })
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.OrderItem
                           .Where(oi => oi.Item.Name.StartsWith(searchString) ||
                                oi.Item.Brand.Name.StartsWith(searchString) || oi.Item.Type.Name.StartsWith(searchString))
                           .Count();
        }

        public List<OrderItem> GetUserOrderItemsNotOrdered(string userId)
        {
            return _context.OrderItem
                           .Include(i => i.Item).Where(oi => oi.UserId == userId && oi.OrderId == null)
                           .ToList();
        }

        public OrderItem GetOrderItemDetailsById(int orderItemId)
        {
            return _context.OrderItem
                           .Include(i => i.Item)
                           .FirstOrDefault(oi => oi.Id == orderItemId);
        }
    }
}
