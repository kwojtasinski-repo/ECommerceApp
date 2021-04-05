using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests
{
    public class OrderRepositoryTests
    {
        [Fact]
        public void CanReturnOrderFromDb()
        {
            var orderInMemoryDatabase = new List<Order>
            {
                new Order() { Id = 1, Cost = new decimal(200.00) },
                new Order() { Id = 2, Cost = new decimal(300.00) },
                new Order() { Id = 3, Cost = new decimal(500.00) }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderById(It.IsAny<int>())).Returns((int i) => orderInMemoryDatabase.SingleOrDefault(bo => bo.Id == i));
            var repository = mock.Object;

            var orderThatExists = repository.GetOrderById(2);
            orderThatExists.Should().NotBeNull();
            orderThatExists.Should().Be(orderInMemoryDatabase[1]);
            orderThatExists.Should().BeOfType(typeof(Order));
            orderThatExists.Should().BeSameAs(orderInMemoryDatabase[1]);
        }

        [Fact]
        public void CantReturnOrderFromDb()
        {
            var orderInMemoryDatabase = new List<Order>
            {
                new Order() { Id = 1, Cost = new decimal(200.00) },
                new Order() { Id = 2, Cost = new decimal(300.00) },
                new Order() { Id = 3, Cost = new decimal(500.00) }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderById(It.IsAny<int>())).Returns((int i) => orderInMemoryDatabase.SingleOrDefault(bo => bo.Id == i));
            var repository = mock.Object;

            var orderThatExists = repository.GetOrderById(4);
            orderThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnOrdersFromDb()
        {
            var orderInMemoryDatabase = new List<Order>
            {
                new Order() { Id = 1, Cost = new decimal(200.00) },
                new Order() { Id = 2, Cost = new decimal(300.00) },
                new Order() { Id = 3, Cost = new decimal(500.00) }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllOrders()).Returns(orderInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var ordersThatExists = repository.GetAllOrders();
            ordersThatExists.Should().NotBeNull();
            ordersThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnOrdersFromDb()
        {
            var orderInMemoryDatabase = new List<Order>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllOrders()).Returns(orderInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var ordersThatExists = repository.GetAllItems();
            ordersThatExists.Should().NotBeNull();
            ordersThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnOrderItemFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = 1, ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = 3, ItemOrderQuantity = 25}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderItemById(It.IsAny<int>())).Returns((int i) => orderItemsInMemoryDatabase.SingleOrDefault(bo => bo.Id == i));
            var repository = mock.Object;

            var orderItemThatExists = repository.GetOrderItemById(1);
            orderItemThatExists.Should().NotBeNull();
            orderItemThatExists.Should().Be(orderItemsInMemoryDatabase[0]);
            orderItemThatExists.Should().BeOfType(typeof(OrderItem));
            orderItemThatExists.Should().BeSameAs(orderItemsInMemoryDatabase[0]);
        }

        [Fact]
        public void CantReturnOrderItemFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = 1, ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = 3, ItemOrderQuantity = 25}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderItemById(It.IsAny<int>())).Returns((int i) => orderItemsInMemoryDatabase.SingleOrDefault(bo => bo.Id == i));
            var repository = mock.Object;

            var orderItemThatExists = repository.GetOrderItemById(4);
            orderItemThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnOrderItemNotOrderedFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = null, ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = null, ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = null, ItemOrderQuantity = 25}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderItemNotOrdered(It.IsAny<OrderItem>())).Returns((OrderItem oi) => orderItemsInMemoryDatabase.SingleOrDefault(bo => bo.Id == oi.Id && oi.OrderId == null));
            var repository = mock.Object;

            var orderItemThatExists = repository.GetOrderItemNotOrdered(orderItemsInMemoryDatabase[1]);
            orderItemThatExists.Should().NotBeNull();
            orderItemThatExists.Should().Be(orderItemsInMemoryDatabase[1]);
            orderItemThatExists.Should().BeOfType(typeof(OrderItem));
            orderItemThatExists.Should().BeSameAs(orderItemsInMemoryDatabase[1]);
        }

        [Fact]
        public void CantReturnOrderItemNotOrderedFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = null, ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = null, ItemOrderQuantity = 25}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderItemNotOrdered(It.IsAny<OrderItem>())).Returns((OrderItem oi) => orderItemsInMemoryDatabase.SingleOrDefault(bo => bo.Id == oi.Id && oi.OrderId == null));
            var repository = mock.Object;

            var orderItemThatExists = repository.GetOrderItemNotOrdered(orderItemsInMemoryDatabase[1]);
            orderItemThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnOrderItemNotOrderedByItemIdFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = null, UserId = "3QEG2GS", ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = null, UserId = "23GERGS", ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = null, UserId = "43GR342", ItemOrderQuantity = 25}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderItemNotOrderedByItemId(It.IsAny<int>(), It.IsAny<string>())).Returns((int itemId, string userId) => orderItemsInMemoryDatabase.SingleOrDefault(bo => bo.ItemId == itemId && bo.UserId == userId && bo.OrderId == null));
            var repository = mock.Object;

            var orderItemThatExists = repository.GetOrderItemNotOrderedByItemId(2, "23GERGS");
            orderItemThatExists.Should().NotBeNull();
            orderItemThatExists.Should().Be(orderItemsInMemoryDatabase[1]);
            orderItemThatExists.Should().BeOfType(typeof(OrderItem));
            orderItemThatExists.Should().BeSameAs(orderItemsInMemoryDatabase[1]);
        }

        [Fact]
        public void CantReturnOrderItemNotOrderedByItemIdFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = null, UserId = "3QEG2GS", ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, UserId = "23GERGS", ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = null, UserId = "43GR342", ItemOrderQuantity = 25}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetOrderItemNotOrderedByItemId(It.IsAny<int>(), It.IsAny<string>())).Returns((int itemId, string userId) => orderItemsInMemoryDatabase.SingleOrDefault(bo => bo.ItemId == itemId && bo.UserId == userId && bo.OrderId == null));
            var repository = mock.Object;

            var orderItemThatExists = repository.GetOrderItemNotOrderedByItemId(2, "23GERGS");
            orderItemThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnOrderItemsByOrderIdFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = 1, UserId = "3QEG2GS", ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, UserId = "23GERGS", ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = 3, UserId = "43GR342", ItemOrderQuantity = 25},
                new OrderItem() { Id = 4, ItemId = 4, OrderId = 3, UserId = "43GR342", ItemOrderQuantity = 10}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllOrderItemsByOrderId(It.IsAny<int>())).Returns((int orderId) => orderItemsInMemoryDatabase.Where(oi => oi.OrderId == orderId).AsQueryable());
            var repository = mock.Object;

            var orderItemsThatExists = repository.GetAllOrderItemsByOrderId(3);
            orderItemsThatExists.Should().NotBeNull();
            orderItemsThatExists.Should().HaveCount(2);
        }

        [Fact]
        public void CantReturnOrderItemsByOrderIdFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem> 
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = null, UserId = "3QEG2GS", ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, UserId = "23GERGS", ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = 3, UserId = "43GR342", ItemOrderQuantity = 25},
                new OrderItem() { Id = 4, ItemId = 4, OrderId = 3, UserId = "43GR342", ItemOrderQuantity = 10}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllOrderItemsByOrderId(It.IsAny<int>())).Returns((int orderId) => orderItemsInMemoryDatabase.Where(oi => oi.OrderId == orderId).AsQueryable());
            var repository = mock.Object;

            var orderItemsThatExists = repository.GetAllOrderItemsByOrderId(1);
            orderItemsThatExists.Should().NotBeNull();
            orderItemsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnOrderItemsFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>
            {
                new OrderItem() { Id = 1, ItemId = 1, OrderId = 1, UserId = "3QEG2GS", ItemOrderQuantity = 5},
                new OrderItem() { Id = 2, ItemId = 2, OrderId = 2, UserId = "23GERGS", ItemOrderQuantity = 15},
                new OrderItem() { Id = 3, ItemId = 3, OrderId = 3, UserId = "43GR342", ItemOrderQuantity = 25},
                new OrderItem() { Id = 4, ItemId = 4, OrderId = 3, UserId = "43GR342", ItemOrderQuantity = 10}
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllOrderItems()).Returns(orderItemsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var orderItemsThatExists = repository.GetAllOrderItems();
            orderItemsThatExists.Should().NotBeNull();
            orderItemsThatExists.Should().HaveCount(4);
        }

        [Fact]
        public void CantReturnOrderItemsFromDb()
        {
            var orderItemsInMemoryDatabase = new List<OrderItem>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllOrderItems()).Returns(orderItemsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var orderItemsThatExists = repository.GetAllOrderItems();
            orderItemsThatExists.Should().NotBeNull();
            orderItemsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnPaymentFromDb()
        {
            var paymentsInMemoryDatabase = new List<Payment>
            {
                new Payment() { Id = 1, CustomerId = 1, OrderId = 1, Number = 2523523 },
                new Payment() { Id = 2, CustomerId = 2, OrderId = 2, Number = 7789665 },
                new Payment() { Id = 3, CustomerId = 2, OrderId = 2, Number = 5235232 },
                new Payment() { Id = 4, CustomerId = 3, OrderId = 3, Number = 2565742 },
                new Payment() { Id = 5, CustomerId = 4, OrderId = 4, Number = 3465463 },
                new Payment() { Id = 6, CustomerId = 5, OrderId = 5, Number = 2353463 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetPaymentById(It.IsAny<int>())).Returns((int paymentId) => paymentsInMemoryDatabase.SingleOrDefault(p => p.Id == paymentId));
            var repository = mock.Object;

            var paymentThatExists = repository.GetPaymentById(4);
            paymentThatExists.Should().NotBeNull();
            paymentThatExists.Should().Be(paymentsInMemoryDatabase[3]);
            paymentThatExists.Should().BeOfType(typeof(Payment));
            paymentThatExists.Should().BeSameAs(paymentsInMemoryDatabase[3]);
        }

        [Fact]
        public void CantReturnPaymentFromDb()
        {
            var paymentsInMemoryDatabase = new List<Payment>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetPaymentById(It.IsAny<int>())).Returns((int paymentId) => paymentsInMemoryDatabase.SingleOrDefault(p => p.Id == paymentId));
            var repository = mock.Object;

            var paymentThatExists = repository.GetPaymentById(4);
            paymentThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnPaymentsFromDb()
        {
            var paymentsInMemoryDatabase = new List<Payment>
            {
                new Payment() { Id = 1, CustomerId = 1, OrderId = 1, Number = 2523523 },
                new Payment() { Id = 2, CustomerId = 2, OrderId = 2, Number = 7789665 },
                new Payment() { Id = 3, CustomerId = 2, OrderId = 2, Number = 5235232 },
                new Payment() { Id = 4, CustomerId = 3, OrderId = 3, Number = 2565742 },
                new Payment() { Id = 5, CustomerId = 4, OrderId = 4, Number = 3465463 },
                new Payment() { Id = 6, CustomerId = 5, OrderId = 5, Number = 2353463 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllPayments()).Returns(paymentsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var paymentsThatExists = repository.GetAllPayments();
            paymentsThatExists.Should().NotBeNull();
            paymentsThatExists.Should().HaveCount(6);
        }

        [Fact]
        public void CantReturnPaymentsFromDb()
        {
            var paymentsInMemoryDatabase = new List<Payment>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllPayments()).Returns(paymentsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var paymentsThatExists = repository.GetAllPayments();
            paymentsThatExists.Should().NotBeNull();
            paymentsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnRefundFromDb()
        {
            var refundsInMemoryDatabase = new List<Refund>
            {
                new Refund() { Id = 1, CustomerId = 1, OrderId = 1 },
                new Refund() { Id = 2, CustomerId = 2, OrderId = 2 },
                new Refund() { Id = 3, CustomerId = 3, OrderId = 3 },
                new Refund() { Id = 4, CustomerId = 4, OrderId = 4 },
                new Refund() { Id = 5, CustomerId = 5, OrderId = 5 },
                new Refund() { Id = 6, CustomerId = 6, OrderId = 6 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetRefundById(It.IsAny<int>())).Returns((int refundId) => refundsInMemoryDatabase.SingleOrDefault(p => p.Id == refundId));
            var repository = mock.Object;

            var refundThatExists = repository.GetRefundById(6);
            refundThatExists.Should().NotBeNull();
            refundThatExists.Should().Be(refundsInMemoryDatabase[5]);
            refundThatExists.Should().BeOfType(typeof(Refund));
            refundThatExists.Should().BeSameAs(refundsInMemoryDatabase[5]);
        }

        [Fact]
        public void CantReturnRefundFromDb()
        {
            var refundsInMemoryDatabase = new List<Refund>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetRefundById(It.IsAny<int>())).Returns((int refundId) => refundsInMemoryDatabase.SingleOrDefault(p => p.Id == refundId));
            var repository = mock.Object;

            var paymentThatExists = repository.GetPaymentById(4);
            paymentThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnRefundsFromDb()
        {
            var refundsInMemoryDatabase = new List<Refund>
            {
                new Refund() { Id = 1, CustomerId = 1, OrderId = 1 },
                new Refund() { Id = 2, CustomerId = 2, OrderId = 2 },
                new Refund() { Id = 3, CustomerId = 3, OrderId = 3 },
                new Refund() { Id = 4, CustomerId = 4, OrderId = 4 },
                new Refund() { Id = 5, CustomerId = 5, OrderId = 5 },
                new Refund() { Id = 6, CustomerId = 6, OrderId = 6 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllRefunds()).Returns(refundsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var refundsThatExists = repository.GetAllRefunds();
            refundsThatExists.Should().NotBeNull();
            refundsThatExists.Should().HaveCount(6);
        }

        [Fact]
        public void CantReturnRefundsFromDb()
        {
            var paymentsInMemoryDatabase = new List<Refund>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllRefunds()).Returns(paymentsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var refundsThatExists = repository.GetAllRefunds();
            refundsThatExists.Should().NotBeNull();
            refundsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnItemsFromDbAsQueryable()
        {
            var itemsInMemoryDatabase = new List<Item>
            {
                new Item() { Id = 1, Name = "Samsung", BrandId = 1, TypeId = 1 },
                new Item() { Id = 2, Name = "iPhone", BrandId = 2, TypeId = 1 },
                new Item() { Id = 3, Name = "Xiaomi", BrandId = 3, TypeId = 1 },
                new Item() { Id = 4, Name = "LG", BrandId = 4, TypeId = 1 },
                new Item() { Id = 5, Name = "Realme", BrandId = 5, TypeId = 1 },
                new Item() { Id = 6, Name = "Pocopohone", BrandId = 6, TypeId = 1 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllItems()).Returns(itemsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var itemsThatExists = repository.GetAllItems();
            itemsThatExists.Should().NotBeNull();
            itemsThatExists.Should().HaveCount(6);
        }

        [Fact]
        public void CantReturnItemsFromDbAsQueryable()
        {
            var itemsInMemoryDatabase = new List<Item>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllItems()).Returns(itemsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var itemsThatExists = repository.GetAllItems();
            itemsThatExists.Should().NotBeNull();
            itemsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCustomerByIdOrderFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Test" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Mart", NIP = "34634664" },
                new Customer() { Id = 3, FirstName = "Jenifer", LastName = "Stwar"  },
                new Customer() { Id = 4, FirstName = "Nancy", LastName = "Garget"  },
                new Customer() { Id = 5, FirstName = "Kevin", LastName = "Marhet", NIP = "54364366"  },
                new Customer() { Id = 6, FirstName = "Mike", LastName = "Lekarstwo", NIP = "25463423"  }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetCustomerById(It.IsAny<int>())).Returns((int id) => customersInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var customerThatExists = repository.GetCustomerById(4);
            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeSameAs(customersInMemoryDatabase[3]);
            customerThatExists.Should().Be(customersInMemoryDatabase[3]);
            customerThatExists.Should().BeOfType(typeof(Customer));
        }

        [Fact]
        public void CantReturnCustomerByIdOrderFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Test" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Mart", NIP = "34634664" },
                new Customer() { Id = 3, FirstName = "Jenifer", LastName = "Stwar"  },
                new Customer() { Id = 4, FirstName = "Nancy", LastName = "Garget"  },
                new Customer() { Id = 5, FirstName = "Kevin", LastName = "Marhet", NIP = "54364366"  },
                new Customer() { Id = 6, FirstName = "Mike", LastName = "Lekarstwo", NIP = "25463423"  }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetCustomerById(It.IsAny<int>())).Returns((int id) => customersInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var customerThatExists = repository.GetCustomerById(8);
            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCustomersByUserIdOrderFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Test", UserId = "232gzgsd" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Mart", NIP = "34634664", UserId = "GE214SF" },
                new Customer() { Id = 3, FirstName = "Jenifer", LastName = "Stwar", UserId = "232gzgsd" },
                new Customer() { Id = 4, FirstName = "Nancy", LastName = "Garget", UserId = "23556drd" },
                new Customer() { Id = 5, FirstName = "Kevin", LastName = "Marhet", NIP = "54364366", UserId = "GE214SF" },
                new Customer() { Id = 6, FirstName = "Mike", LastName = "Lekarstwo", NIP = "25463423", UserId = "23556drd" }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetCustomersByUserId(It.IsAny<string>())).Returns((string userId) => customersInMemoryDatabase.Where(c => c.UserId == userId).AsQueryable());
            var repository = mock.Object;

            var customersThatExists = repository.GetCustomersByUserId("GE214SF");
            customersThatExists.Should().NotBeNull();
            customersThatExists.Should().HaveCount(2);
        }

        [Fact]
        public void CantReturnCustomersByUserIdOrderFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Test" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Mart", NIP = "34634664" },
                new Customer() { Id = 3, FirstName = "Jenifer", LastName = "Stwar"  },
                new Customer() { Id = 4, FirstName = "Nancy", LastName = "Garget"  },
                new Customer() { Id = 5, FirstName = "Kevin", LastName = "Marhet", NIP = "54364366"  },
                new Customer() { Id = 6, FirstName = "Mike", LastName = "Lekarstwo", NIP = "25463423"  }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetCustomersByUserId(It.IsAny<string>())).Returns((string userId) => customersInMemoryDatabase.Where(c => c.UserId == userId).AsQueryable());
            var repository = mock.Object;

            var customersThatExists = repository.GetCustomersByUserId("");
            customersThatExists.Should().NotBeNull();
            customersThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCustomersFromDbAsQueryable()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Test" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Mart", NIP = "34634664" },
                new Customer() { Id = 3, FirstName = "Jenifer", LastName = "Stwar"  },
                new Customer() { Id = 4, FirstName = "Nancy", LastName = "Garget"  },
                new Customer() { Id = 5, FirstName = "Kevin", LastName = "Marhet", NIP = "54364366"  },
                new Customer() { Id = 6, FirstName = "Mike", LastName = "Lekarstwo", NIP = "25463423"  }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllCustomers()).Returns(customersInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var customersThatExists = repository.GetAllCustomers();
            customersThatExists.Should().NotBeNull();
            customersThatExists.Should().HaveCount(6);
        }

        [Fact]
        public void CantReturnCustomersFromDbAsQueryable()
        {
            var customersInMemoryDatabase = new List<Customer>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllCustomers()).Returns(customersInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var customersThatExists = repository.GetAllCustomers();
            customersThatExists.Should().NotBeNull();
            customersThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCouponFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "GD#@GDG53466", CouponTypeId = 1 },
                new Coupon() { Id = 2, Code = "GDBSDFBH%743", CouponTypeId = 2 },
                new Coupon() { Id = 3, Code = "FSF#WGsdf214", CouponTypeId = 3 },
                new Coupon() { Id = 4, Code = "@!$@$+41224=", CouponTypeId = 4 },
                new Coupon() { Id = 5, Code = "gDsdgstw32@5", CouponTypeId = 5 },
                new Coupon() { Id = 6, Code = "@T#@%FDWS#65", CouponTypeId = 6 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetCouponById(It.IsAny<int>())).Returns((int itemId) => couponsInMemoryDatabase.SingleOrDefault(i => i.Id == itemId));
            var repository = mock.Object;

            var couponThatExists = repository.GetCouponById(5);
            couponThatExists.Should().NotBeNull();
            couponThatExists.Should().BeSameAs(couponsInMemoryDatabase[4]);
            couponThatExists.Should().Be(couponsInMemoryDatabase[4]);
            couponThatExists.Should().BeOfType(typeof(Coupon));
        }

        [Fact]
        public void CantReturnCouponFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "GD#@GDG53466", CouponTypeId = 1 },
                new Coupon() { Id = 2, Code = "GDBSDFBH%743", CouponTypeId = 2 },
                new Coupon() { Id = 3, Code = "FSF#WGsdf214", CouponTypeId = 3 },
                new Coupon() { Id = 4, Code = "@!$@$+41224=", CouponTypeId = 4 },
                new Coupon() { Id = 5, Code = "gDsdgstw32@5", CouponTypeId = 5 },
                new Coupon() { Id = 6, Code = "@T#@%FDWS#65", CouponTypeId = 6 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetCouponById(It.IsAny<int>())).Returns((int itemId) => couponsInMemoryDatabase.SingleOrDefault(i => i.Id == itemId));
            var repository = mock.Object;

            var couponThatExists = repository.GetCouponById(8);
            couponThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCouponsFromDbAsQueryable()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "GD#@GDG53466", CouponTypeId = 1 },
                new Coupon() { Id = 2, Code = "GDBSDFBH%743", CouponTypeId = 2 },
                new Coupon() { Id = 3, Code = "FSF#WGsdf214", CouponTypeId = 3 },
                new Coupon() { Id = 4, Code = "@!$@$+41224=", CouponTypeId = 4 },
                new Coupon() { Id = 5, Code = "gDsdgstw32@5", CouponTypeId = 5 },
                new Coupon() { Id = 6, Code = "@T#@%FDWS#65", CouponTypeId = 6 }
            };

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllCoupons()).Returns(couponsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponsThatExists = repository.GetAllCoupons();
            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(6);
        }

        [Fact]
        public void CantReturnCouponsFromDbAsQueryable()
        {
            var couponsInMemoryDatabase = new List<Coupon>();

            var mock = new Mock<IOrderRepository>();
            mock.Setup(x => x.GetAllCoupons()).Returns(couponsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponsThatExists = repository.GetAllCoupons();
            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(0);
        }
    }
}
