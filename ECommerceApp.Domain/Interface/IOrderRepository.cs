using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository
    {
        void DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
    }
}
