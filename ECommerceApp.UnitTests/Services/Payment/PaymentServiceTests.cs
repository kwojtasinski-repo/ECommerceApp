using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.CurrencyRate;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using ECommerceApp.Application.Services.Orders;

namespace ECommerceApp.UnitTests.Services.Payment
{
    public class PaymentServiceTests : BaseTest
    {
        private readonly Mock<IPaymentRepository> _paymentRepository;
        private readonly Mock<IOrderService> _orderService;
        private readonly Mock<ICustomerService> _customerService;
        private readonly Mock<ICurrencyRateService> _currencyRateService;
        private readonly HttpContextAccessorTest _contextAccessor;

        public PaymentServiceTests()
        {
            _paymentRepository = new Mock<IPaymentRepository>();
            _orderService = new Mock<IOrderService>();
            _customerService = new Mock<ICustomerService>();
            _currencyRateService = new Mock<ICurrencyRateService>();
            _contextAccessor = new HttpContextAccessorTest();
        }

        [Fact]
        public void given_valid_payment_should_add()
        {
            var id = 1;
            var currencyId = 2;
            var orderId = 1;
            var payment = CreatePaymentVm(id, currencyId, orderId);
            var cost = payment.OrderCost;
            payment.Id = 0;
            var order = CreateOrder(orderId);
            _orderService.Setup(o => o.Get(orderId)).Returns(order);
            var rate = CreateCurrencyRate(currencyId);
            _currencyRateService.Setup(cr => cr.GetLatestRate(currencyId)).Returns(rate);
            var paymentService = new PaymentService(_paymentRepository.Object, _mapper, _orderService.Object, _customerService.Object, _currencyRateService.Object, _contextAccessor);

            paymentService.AddPayment(payment);

            order.Cost.Should().BeLessThan(cost);
            _paymentRepository.Verify(p => p.AddPayment(It.IsAny<Domain.Model.Payment>()), Times.Once);
            _orderService.Verify(p => p.Update(It.IsAny<OrderVm>()), Times.Once);
        }

        [Fact]
        public void given_invalid_payment_should_add()
        {
            var id = 1;
            var currencyId = 2;
            var orderId = 1;
            var payment = CreatePaymentVm(id, currencyId, orderId);
            var paymentService = new PaymentService(_paymentRepository.Object, _mapper, _orderService.Object, _customerService.Object, _currencyRateService.Object, _contextAccessor);

            Action action = () => paymentService.AddPayment(payment);

            action.Should().Throw<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_id_payment_should_exists()
        {
            var id = 1;
            var currencyId = 1;
            var orderId = 1;
            var payment = CreatePayment(id, currencyId, orderId);
            _paymentRepository.Setup(p => p.GetById(id)).Returns(payment);
            var paymentService = new PaymentService(_paymentRepository.Object, _mapper, _orderService.Object, _customerService.Object, _currencyRateService.Object, _contextAccessor);

            var exists = paymentService.PaymentExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_id_payment_shouldnt_exists()
        {
            var id = 1;
            var paymentService = new PaymentService(_paymentRepository.Object, _mapper, _orderService.Object, _customerService.Object, _currencyRateService.Object, _contextAccessor);

            var exists = paymentService.PaymentExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_payment_should_update()
        {
            int id = 1;
            var currencyId = 1;
            var orderId = 1;
            var payment = CreatePaymentVm(id, currencyId, orderId);
            var paymentService = new PaymentService(_paymentRepository.Object, _mapper, _orderService.Object, _customerService.Object, _currencyRateService.Object, _contextAccessor);

            paymentService.UpdatePayment(payment);

            _paymentRepository.Verify(p => p.UpdatePayment(It.IsAny<Domain.Model.Payment>()), Times.Once);
        }

        [Fact]
        public void given_null_payment_when_add_should_throw_an_exception()
        {
            var paymentService = new PaymentService(_paymentRepository.Object, _mapper, _orderService.Object, _customerService.Object, _currencyRateService.Object, _contextAccessor);

            Action action = () => paymentService.AddPayment(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private PaymentVm CreatePaymentVm(int id, int currencyId, int orderId)
        {
            var payment = new PaymentVm
            {
                Id = id,
                CurrencyId = currencyId,
                OrderId = orderId,
                CustomerId = 1,
                Number = 1234,
                OrderNumber = 124,
                OrderCost = new decimal(100)
            };
            return payment;
        }

        private Domain.Model.Payment CreatePayment(int id, int currencyId, int orderId)
        {
            var payment = new Domain.Model.Payment
            {
                Id = id,
                CurrencyId = currencyId,
                OrderId = orderId,
                CustomerId = 1,
                Number = 1234
            };
            return payment;
        }

        private OrderVm CreateOrder(int orderId)
        {
            var order = new OrderVm
            {
                Id = orderId,
                IsPaid = false,
                Cost = new decimal(100)
            };
            return order;
        }

        private CurrencyRateVm CreateCurrencyRate(int currencyId)
        {
            var currencyRate = new CurrencyRateVm
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
