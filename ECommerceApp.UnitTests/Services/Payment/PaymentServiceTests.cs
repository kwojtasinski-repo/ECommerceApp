using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using ECommerceApp.Application.DTO;

namespace ECommerceApp.UnitTests.Services.Payment
{
    public class PaymentServiceTests : BaseTest
    {
        private readonly PaymentInMemoryRepository _paymentRepository;
        private readonly Mock<IOrderRepository> _orderRepository;
        private readonly Mock<ICustomerService> _customerService;
        private readonly Mock<ICurrencyRateService> _currencyRateService;
        private readonly Mock<ICurrencyRepository> _currencyRepository;
        private readonly UserContextTest _userContext;
        private readonly IPaymentHandler _paymentHandler;

        public PaymentServiceTests()
        {
            _paymentRepository = new PaymentInMemoryRepository();
            _orderRepository = new Mock<IOrderRepository>();
            _customerService = new Mock<ICustomerService>();
            _currencyRateService = new Mock<ICurrencyRateService>();
            _currencyRepository = new Mock<ICurrencyRepository>();
            _userContext = new UserContextTest();
            _paymentHandler = new PaymentHandler(_paymentRepository, _currencyRateService.Object, _currencyRepository.Object);
            _currencyRepository.Setup(c => c.GetById(It.IsAny<int>())).Returns(new Domain.Model.Currency
            {
                Id = 1,
                Code = "PLN"
            });
        }

        private PaymentService CreateService() 
            => new (_paymentRepository, _mapper, _orderRepository.Object, _customerService.Object, _userContext, _paymentHandler);

        [Fact]
        public void given_valid_payment_should_add()
        {
            var currencyId = 2;
            var orderId = 1;
            var payment = new AddPaymentDto { CurrencyId = currencyId, OrderId = orderId };
            var cost = 100M;
            var order = AddOrder(orderId);
            var rate = AddRate(currencyId);
            var paymentService = CreateService();

            var id = paymentService.AddPayment(payment);

            var orderUpdated = _orderRepository.Object.GetOrderById(orderId);
            orderUpdated.IsPaid.Should().BeTrue();
            var paymentAdded = _paymentRepository.GetPaymentById(id);
            paymentAdded.Cost.Should().BeLessThan(cost);
            paymentAdded.Cost.Should().Be(cost/rate.Rate);
            paymentAdded.CurrencyId.Should().Be(currencyId);
            _orderRepository.Verify(p => p.UpdatedOrder(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_valid_payment_should_update()
        {
            var currencyId = 1;
            var orderId = 1;
            var payment = CreatePayment(currencyId, orderId);
            var id = _paymentRepository.AddPayment(payment);
            var vm = new PaymentVm { Id = id, CurrencyId = 2, OrderId = orderId, Number = payment.Number, Cost = payment.Cost, CustomerId = payment.CustomerId, DateOfOrderPayment = payment.DateOfOrderPayment, State = payment.State };
            var paymentService = CreateService();

            paymentService.UpdatePayment(vm);

            var paymentUpdated = _paymentRepository.GetPaymentById(id);
            paymentUpdated.Should().NotBeNull();
            paymentUpdated.CurrencyId.Should().Be(vm.CurrencyId);
        }

        [Fact]
        public void given_null_payment_when_add_should_throw_an_exception()
        {
            var paymentService = CreateService();

            Action action = () => paymentService.AddPayment(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private static Domain.Model.Payment CreatePayment(int currencyId, int orderId)
        {
            var payment = new Domain.Model.Payment
            {
                CurrencyId = currencyId,
                OrderId = orderId,
                CustomerId = 1,
                Number = "1234",
                Cost = new decimal(100),
            };
            return payment;
        }

        private OrderDto AddOrder(int orderId)
        {
            var order = CreateOrder(orderId);
            _orderRepository.Setup(o => o.GetOrderById(orderId)).Returns(_mapper.Map<Domain.Model.Order>(order));
            return order;
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

        private CurrencyRateDto AddRate(int currencyId)
        {
            var rate = CreateCurrencyRate(currencyId);
            _currencyRateService.Setup(cr => cr.GetLatestRate(currencyId)).Returns(rate);
            return rate;
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
