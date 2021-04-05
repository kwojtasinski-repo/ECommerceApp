using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Repositories.CouponRepository
{
    public class CouponRepositoryTests
    {
        [Fact]
        public void CanReturnCouponByIdFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "sdgsdg3@GDSG" },
                new Coupon() { Id = 2, Code = "KLNLGNL@$FA" },
                new Coupon() { Id = 3, Code = "2353DGSBH@#" }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponById(It.IsAny<int>())).Returns((int id) => couponsInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var couponThatExists = repository.GetCouponById(3);
            couponThatExists.Should().NotBeNull();
            couponThatExists.Should().BeSameAs(couponsInMemoryDatabase[2]);
            couponThatExists.Should().Be(couponsInMemoryDatabase[2]);
            couponThatExists.Should().BeOfType(typeof(Coupon));
        }

        [Fact]
        public void CantReturnCouponByIdFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "sdgsdg3@GDSG" },
                new Coupon() { Id = 2, Code = "KLNLGNL@$FA" },
                new Coupon() { Id = 3, Code = "2353DGSBH@#" }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponById(It.IsAny<int>())).Returns((int id) => couponsInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var couponThatExists = repository.GetCouponById(10);
            couponThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCouponsFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "sdgsdg3@GDSG" },
                new Coupon() { Id = 2, Code = "KLNLGNL@$FA" },
                new Coupon() { Id = 3, Code = "2353DGSBH@#" }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCoupons()).Returns(couponsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponsThatExists = repository.GetAllCoupons();
            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnCouponsFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>();

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCoupons()).Returns(couponsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponsThatExists = repository.GetAllCoupons();
            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCouponTypeByIdFromDb()
        {
            var couponTypesInMemoryDatabase = new List<CouponType>
            {
                new CouponType() { Id = 1, Type = "Jewelry" },
                new CouponType() { Id = 2, Type = "Clothes" },
                new CouponType() { Id = 3, Type = "Electronic" }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponTypeById(It.IsAny<int>())).Returns((int id) => couponTypesInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var couponThatExists = repository.GetCouponTypeById(1);
            couponThatExists.Should().NotBeNull();
            couponThatExists.Should().BeSameAs(couponTypesInMemoryDatabase[0]);
            couponThatExists.Should().Be(couponTypesInMemoryDatabase[0]);
            couponThatExists.Should().BeOfType(typeof(CouponType));
        }

        [Fact]
        public void CantReturnCouponTypeByIdFromDb()
        {
            var couponTypesInMemoryDatabase = new List<CouponType>
            {
                new CouponType() { Id = 1, Type = "Jewelry" },
                new CouponType() { Id = 2, Type = "Clothes" },
                new CouponType() { Id = 3, Type = "Electronic" }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponTypeById(It.IsAny<int>())).Returns((int id) => couponTypesInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var couponThatExists = repository.GetCouponTypeById(10);
            couponThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCouponsByTypeIdFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "sdgsdg3@GDSG", CouponTypeId = 1 },
                new Coupon() { Id = 2, Code = "KLNLGNL@$FA", CouponTypeId = 1 },
                new Coupon() { Id = 3, Code = "2353DGSBH@#", CouponTypeId = 2 },
                new Coupon() { Id = 4, Code = "as253dg2", CouponTypeId = 2 }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponsByCouponTypeId(It.IsAny<int>())).Returns((int typeId) => couponsInMemoryDatabase.Where(c => c.CouponTypeId == typeId).AsQueryable());
            var repository = mock.Object;

            var couponsThatExists = repository.GetCouponsByCouponTypeId(2);
            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(2);
        }

        [Fact]
        public void CantReturnCouponsByTypeIdFromDb()
        {
            var couponsInMemoryDatabase = new List<Coupon>
            {
                new Coupon() { Id = 1, Code = "sdgsdg3@GDSG", CouponTypeId = 1 },
                new Coupon() { Id = 2, Code = "KLNLGNL@$FA", CouponTypeId = 1 },
                new Coupon() { Id = 3, Code = "2353DGSBH@#", CouponTypeId = 2 },
                new Coupon() { Id = 4, Code = "as253dg2", CouponTypeId = 2 }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponsByCouponTypeId(It.IsAny<int>())).Returns((int typeId) => couponsInMemoryDatabase.Where(c => c.CouponTypeId == typeId).AsQueryable());
            var repository = mock.Object;

            var couponsThatExists = repository.GetCouponsByCouponTypeId(3);
            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCouponTypesFromDb()
        {
            var couponTypesInMemoryDatabase = new List<CouponType>
            {
                new CouponType() { Id = 1, Type = "Jewelry" },
                new CouponType() { Id = 2, Type = "Clothes" },
                new CouponType() { Id = 3, Type = "Electronic" }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCouponsTypes()).Returns(couponTypesInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponTypesThatExists = repository.GetAllCouponsTypes();
            couponTypesThatExists.Should().NotBeNull();
            couponTypesThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnCouponTypesFromDb()
        {
            var couponTypesInMemoryDatabase = new List<CouponType>();

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCouponsTypes()).Returns(couponTypesInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponTypesThatExists = repository.GetAllCouponsTypes();
            couponTypesThatExists.Should().NotBeNull();
            couponTypesThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCouponUsedByIdFromDb()
        {
            var couponsUsedInMemoryDatabase = new List<CouponUsed>
            {
                new CouponUsed() { Id = 1, CouponId = 1, OrderId = 1 },
                new CouponUsed() { Id = 2, CouponId = 2, OrderId = 2 },
                new CouponUsed() { Id = 3, CouponId = 3, OrderId = 3 }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponUsedById(It.IsAny<int>())).Returns((int id) => couponsUsedInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var couponUsedThatExists = repository.GetCouponUsedById(1);
            couponUsedThatExists.Should().NotBeNull();
            couponUsedThatExists.Should().BeSameAs(couponsUsedInMemoryDatabase[0]);
            couponUsedThatExists.Should().Be(couponsUsedInMemoryDatabase[0]);
            couponUsedThatExists.Should().BeOfType(typeof(CouponUsed));
        }

        [Fact]
        public void CantReturnCouponUsedByIdFromDb()
        {
            var couponsUsedInMemoryDatabase = new List<CouponUsed>
            {
                new CouponUsed() { Id = 1, CouponId = 1, OrderId = 1 },
                new CouponUsed() { Id = 2, CouponId = 2, OrderId = 2 },
                new CouponUsed() { Id = 3, CouponId = 3, OrderId = 3 }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetCouponUsedById(It.IsAny<int>())).Returns((int id) => couponsUsedInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var couponUsedThatExists = repository.GetCouponUsedById(10);
            couponUsedThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCouponsUsedFromDb()
        {
            var couponsUsedInMemoryDatabase = new List<CouponUsed>
            {
                new CouponUsed() { Id = 1, CouponId = 1, OrderId = 1 },
                new CouponUsed() { Id = 2, CouponId = 2, OrderId = 2 },
                new CouponUsed() { Id = 3, CouponId = 3, OrderId = 3 }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCouponsUsed()).Returns(couponsUsedInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponsUsedThatExists = repository.GetAllCouponsUsed();
            couponsUsedThatExists.Should().NotBeNull();
            couponsUsedThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnCouponsUsedFromDb()
        {
            var couponsUsedInMemoryDatabase = new List<CouponUsed>();

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCouponsUsed()).Returns(couponsUsedInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var couponsUsedThatExists = repository.GetAllCouponsUsed();
            couponsUsedThatExists.Should().NotBeNull();
            couponsUsedThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnCouponsUsedByTypeIdFromDb()
        {
            var couponUsedInMemoryDatabase = new List<CouponUsed>
            {
                new CouponUsed() { Id = 1, CouponId = 1, Coupon = new Coupon {Id = 1, Code = "SF2fg23", CouponTypeId = 1 }},
                new CouponUsed() { Id = 2, CouponId = 2, Coupon = new Coupon {Id = 2, Code = "SaF@W@F", CouponTypeId = 1 }}, 
                new CouponUsed() { Id = 3, CouponId = 3, Coupon = new Coupon {Id = 3, Code = "SACVDSBVDS23", CouponTypeId = 2 }},
                new CouponUsed() { Id = 4, CouponId = 4, Coupon = new Coupon {Id = 4, Code = "SAGGW#23", CouponTypeId = 2 }}
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCouponsUsedType(It.IsAny<int>())).Returns((int couponTypeId) => couponUsedInMemoryDatabase.Where(c => c.Coupon.CouponTypeId == couponTypeId).AsQueryable());
            var repository = mock.Object;

            var couponsUsedThatExists = repository.GetAllCouponsUsedType(2);
            couponsUsedThatExists.Should().NotBeNull();
            couponsUsedThatExists.Should().HaveCount(2);
        }

        [Fact]
        public void CantReturnCouponsUsedByTypeIdFromDb()
        {
            var couponUsedInMemoryDatabase = new List<CouponUsed>();

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllCouponsUsedType(It.IsAny<int>())).Returns((int couponTypeId) => couponUsedInMemoryDatabase.Where(c => c.Coupon.CouponTypeId == couponTypeId).AsQueryable());
            var repository = mock.Object;

            var couponsUsedThatExists = repository.GetAllCouponsUsedType(1);
            couponsUsedThatExists.Should().NotBeNull();
            couponsUsedThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnOrdersWithCouponsFromDb()
        {
            var ordersWithCouponsInMemoryDatabase = new List<Order>
            {
                new Order() { Id = 1, Cost = new decimal(120.24), CouponUsedId = 1, Number = 124234 },
                new Order() { Id = 2, Cost = new decimal(25220.24), CouponUsedId = 2, Number = 56864 },
                new Order() { Id = 3, Cost = new decimal(12021.44), CouponUsedId = 3, Number = 463345 }
            };

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllOrders()).Returns(ordersWithCouponsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var ordersThatExists = repository.GetAllOrders();
            ordersThatExists.Should().NotBeNull();
            ordersThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnOrdersWithCouponsFromDb()
        {
            var ordersWithCouponsInMemoryDatabase = new List<Order>();

            var mock = new Mock<ICouponRepository>();
            mock.Setup(x => x.GetAllOrders()).Returns(ordersWithCouponsInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var ordersThatExists = repository.GetAllCouponsTypes();
            ordersThatExists.Should().NotBeNull();
            ordersThatExists.Should().HaveCount(0);
        }
    }
}
