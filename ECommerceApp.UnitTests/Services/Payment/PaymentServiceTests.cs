using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;

namespace ECommerceApp.UnitTests.Services.Payment
{
    public class PaymentServiceTests : BaseTest
    {
        private readonly Mock<IPaymentRepository> _paymentRepository;
        private readonly Mock<IOrderRepository> _orderRepository;
        private readonly Mock<ICustomerService> _customerService;
        private readonly Mock<ICurrencyRateService> _currencyRateService;
        private readonly HttpContextAccessorTest _contextAccessor;
        private readonly IPaymentHandler _paymentHandler;

        public PaymentServiceTests()
        {
            _paymentRepository = new Mock<IPaymentRepository>();
            _orderRepository = new Mock<IOrderRepository>();
            _customerService = new Mock<ICustomerService>();
            _currencyRateService = new Mock<ICurrencyRateService>();
            _contextAccessor = new HttpContextAccessorTest();
            _paymentHandler = new PaymentHandler(_paymentRepository.Object, _currencyRateService.Object);
        }

        private PaymentService CreateService() 
            => new (_paymentRepository.Object, _mapper, _orderRepository.Object, _customerService.Object, _contextAccessor, _paymentHandler);

        [Fact]
        public void given_valid_payment_should_add()
        {
            var currencyId = 2;
            var orderId = 1;
            var payment = new AddPaymentDto { CurrencyId = currencyId, OrderId = orderId };
            var cost = 100M;
            var order = CreateOrder(orderId);
            _orderRepository.Setup(o => o.GetById(orderId)).Returns(_mapper.Map<Domain.Model.Order>(order));
            var rate = CreateCurrencyRate(currencyId);
            _currencyRateService.Setup(cr => cr.GetLatestRate(currencyId)).Returns(rate);
            Domain.Model.Payment paymentAdded = null;
            _paymentRepository.Setup(p => p.AddPayment(It.IsAny<Domain.Model.Payment>()))
                .Callback((Domain.Model.Payment p) => {
                    paymentAdded = p;
                    });
            var paymentService = CreateService();

            paymentService.AddPayment(payment);

            var orderUpdated = _orderRepository.Object.GetById(orderId);
            orderUpdated.IsPaid.Should().BeTrue();
            paymentAdded.Cost.Should().BeLessThan(cost);
            paymentAdded.Cost.Should().Be(cost/rate.Rate);
            paymentAdded.CurrencyId.Should().Be(currencyId);
            _paymentRepository.Verify(p => p.AddPayment(It.IsAny<Domain.Model.Payment>()), Times.Once);
            _orderRepository.Verify(p => p.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_valid_payment_should_update()
        {
            int id = 1;
            var currencyId = 1;
            var orderId = 1;
            var payment = CreatePaymentVm(id, currencyId, orderId);
            var paymentService = CreateService();

            paymentService.UpdatePayment(payment);

            _paymentRepository.Verify(p => p.UpdatePayment(It.IsAny<Domain.Model.Payment>()), Times.Once);
        }

        [Fact]
        public void given_null_payment_when_add_should_throw_an_exception()
        {
            var paymentService = CreateService();

            Action action = () => paymentService.AddPayment(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private static PaymentVm CreatePaymentVm(int id, int currencyId, int orderId)
        {
            var payment = new PaymentVm
            {
                Id = id,
                CurrencyId = currencyId,
                OrderId = orderId,
                CustomerId = 1,
                Number = "1234",
                OrderNumber = Guid.NewGuid().ToString(),
                Cost = new decimal(100)
            };
            return payment;
        }

        private static Domain.Model.Payment CreatePayment(int id, int currencyId, int orderId)
        {
            var payment = new Domain.Model.Payment
            {
                Id = id,
                CurrencyId = currencyId,
                OrderId = orderId,
                CustomerId = 1,
                Number = "1234"
            };
            return payment;
        }

        private static OrderDto CreateOrder(int orderId)
        {
            var order = new OrderDto
            {
                Id = orderId,
                IsPaid = false,
                Cost = new decimal(100)
            };
            return order;
        }

        private static CurrencyRateDto CreateCurrencyRate(int currencyId)
        {
            var currencyRate = new CurrencyRateDto
            {
                Id = 1,
                CurrencyDate = DateTime.Now,
                CurrencyId = currencyId,
                Rate = new decimal(4)
            };
            return currencyRate;
        }
    }
}
