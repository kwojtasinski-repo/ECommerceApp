using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class OrderController : Controller
    {
        
        public IActionResult OrderList()
        {
            List<Order> orders = new List<Order>();
            
            Order order1 = new Order();
            Order order2 = new Order();
            Order order3 = new Order();
            Order order4 = new Order();
            
            ItemOrder itemOrder1 = new ItemOrder();
            ItemOrder itemOrder2 = new ItemOrder();
            ItemOrder itemOrder3 = new ItemOrder();
            ItemOrder itemOrder4 = new ItemOrder();
            
            Item item1 = new Item() { ItemId = 1, ItemName = "T-Shirt", ItemTypeName = "Clothing", ItemCost = 50.0, ItemDescription = "Podkoszulka marki Gucci", ItemQuantity = 50 };
            Item item2 = new Item() { ItemId = 2, ItemName = "Apple", ItemTypeName = "Grocery", ItemCost = 0.50, ItemDescription = "Jablko swieze", ItemQuantity = 120 };
            Item item3 = new Item() { ItemId = 3, ItemName = "Cup", ItemTypeName = "KitchenUtensils", ItemCost = 15.0, ItemDescription = "Kubek ze sportowymi samochodami", ItemQuantity = 20 };
            Item item4 = new Item() { ItemId = 4, ItemName = "Arduino Leonardo", ItemTypeName = "ElectricalDevices", ItemCost = 90.0, ItemDescription = "Arduino Leonardo z mikrokontrolerem AVR Atmega32U4. Posiada 32 kB pamięci Flash, 2,5 kB RAM, 20 cyfrowych wejść/wyjść", ItemQuantity = 500 };
            
            itemOrder1.Item = item1;
            itemOrder1.ItemOrderQuantity = 1;
            itemOrder1.Order.Add(order1);
            
            itemOrder2.Item = item2;
            itemOrder2.ItemOrderQuantity = 10;
            itemOrder1.Order.Add(order1);
            itemOrder1.Order.Add(order2);

            itemOrder3.Item = item3;
            itemOrder3.ItemOrderQuantity = 5;
            itemOrder1.Order.Add(order3);
            itemOrder1.Order.Add(order4);

            itemOrder4.Item = item4;
            itemOrder4.ItemOrderQuantity = 2;
            itemOrder1.Order.Add(order1);
            itemOrder1.Order.Add(order2);
            itemOrder1.Order.Add(order3);
            itemOrder1.Order.Add(order4);

            order1.OrderId = 1;
            order1.OrderNumber = 2020;
            order1.ItemOrder.Add(itemOrder1);
            CalculateCost(order1);

            order2.OrderId = 2;
            order2.OrderNumber = 2030;
            order2.ItemOrder.Add(itemOrder1);
            order2.ItemOrder.Add(itemOrder2);
            CalculateCost(order2);

            order3.OrderId = 3;
            order3.OrderNumber = 2040;
            order3.ItemOrder.Add(itemOrder3);
            order3.ItemOrder.Add(itemOrder4);
            CalculateCost(order3);

            order4.OrderId = 4;
            order4.OrderNumber = 2120;
            order4.ItemOrder.Add(itemOrder1);
            order4.ItemOrder.Add(itemOrder2);
            order4.ItemOrder.Add(itemOrder3);
            order4.ItemOrder.Add(itemOrder4);
            CalculateCost(order4);

            orders.Add(order1);
            orders.Add(order2);
            orders.Add(order3);
            orders.Add(order4);

            return View(orders);
        }

        public IActionResult OrderDetails(int id)
        {
            List<Order> orders = new List<Order>();

            Order order1 = new Order();
            Order order2 = new Order();
            Order order3 = new Order();
            Order order4 = new Order();

            ItemOrder itemOrder1 = new ItemOrder();
            ItemOrder itemOrder2 = new ItemOrder();
            ItemOrder itemOrder3 = new ItemOrder();
            ItemOrder itemOrder4 = new ItemOrder();

            Item item1 = new Item() { ItemId = 1, ItemName = "T-Shirt", ItemTypeName = "Clothing", ItemCost = 50.0, ItemDescription = "Podkoszulka marki Gucci", ItemQuantity = 50 };
            Item item2 = new Item() { ItemId = 2, ItemName = "Apple", ItemTypeName = "Grocery", ItemCost = 0.50, ItemDescription = "Jablko swieze", ItemQuantity = 120 };
            Item item3 = new Item() { ItemId = 3, ItemName = "Cup", ItemTypeName = "KitchenUtensils", ItemCost = 15.0, ItemDescription = "Kubek ze sportowymi samochodami", ItemQuantity = 20 };
            Item item4 = new Item() { ItemId = 4, ItemName = "Arduino Leonardo", ItemTypeName = "ElectricalDevices", ItemCost = 90.0, ItemDescription = "Arduino Leonardo z mikrokontrolerem AVR Atmega32U4. Posiada 32 kB pamięci Flash, 2,5 kB RAM, 20 cyfrowych wejść/wyjść", ItemQuantity = 500 };

            itemOrder1.Item = item1;
            itemOrder1.ItemOrderQuantity = 1;
            itemOrder1.Order.Add(order1);

            itemOrder2.Item = item2;
            itemOrder2.ItemOrderQuantity = 10;
            itemOrder1.Order.Add(order1);
            itemOrder1.Order.Add(order2);

            itemOrder3.Item = item3;
            itemOrder3.ItemOrderQuantity = 5;
            itemOrder1.Order.Add(order3);
            itemOrder1.Order.Add(order4);

            itemOrder4.Item = item4;
            itemOrder4.ItemOrderQuantity = 2;
            itemOrder1.Order.Add(order1);
            itemOrder1.Order.Add(order2);
            itemOrder1.Order.Add(order3);
            itemOrder1.Order.Add(order4);

            order1.OrderId = 1;
            order1.OrderNumber = 2020;
            order1.ItemOrder.Add(itemOrder1);
            CalculateCost(order1);

            order2.OrderId = 2;
            order2.OrderNumber = 2030;
            order2.ItemOrder.Add(itemOrder1);
            order2.ItemOrder.Add(itemOrder2);
            CalculateCost(order2);

            order3.OrderId = 3;
            order3.OrderNumber = 2040;
            order3.ItemOrder.Add(itemOrder3);
            order3.ItemOrder.Add(itemOrder4);
            CalculateCost(order3);

            order4.OrderId = 4;
            order4.OrderNumber = 2120;
            order4.ItemOrder.Add(itemOrder1);
            order4.ItemOrder.Add(itemOrder2);
            order4.ItemOrder.Add(itemOrder3);
            order4.ItemOrder.Add(itemOrder4);
            CalculateCost(order4);

            orders.Add(order1);
            orders.Add(order2);
            orders.Add(order3);
            orders.Add(order4);

            Order order = orders.FirstOrDefault(o => o.OrderId == id);
            
            return View(order);
        }

        private void CalculateCost(Order order)
        {
            List<ItemOrder> itemOrders = order.ItemOrder.ToList();
            double cost = 0.0;
            itemOrders.ForEach(i => cost += i.Item.ItemCost * i.ItemOrderQuantity);
            order.OrderCost = cost;
        }
    }
}
