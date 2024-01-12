using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Payment
{
    public class PaymentHandlerTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepository;
        private readonly Mock<ICurrencyRateService> _currencyRateService;

        public PaymentHandlerTests()
        {
            _paymentRepository = new Mock<IPaymentRepository>();
            _currencyRateService = new Mock<ICurrencyRateService>();
        }

        private PaymentHandler CreatePaymentHandler()
            => new(_paymentRepository.Object, _currencyRateService.Object);

        [Fact]
        public void given_null_order_when_create_payment_should_throw_an_exception()
        {
            var dto = new AddPaymentDto { OrderId = 1, CurrencyId = 1 };
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.CreatePayment(dto, null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"{typeof(Order).Name} cannot be null");
        }

        [Fact]
        public void given_null_dto_when_create_payment_should_throw_an_exception()
        {
            var order = CreateDefaultOrder();
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.CreatePayment(null, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"{typeof(AddPaymentDto).Name} cannot be null");
        }

        [Fact]
        public void given_paid_order_when_create_payment_should_throw_an_exception()
        {
            var order = CreateDefaultOrder();
            order.IsPaid = true;
            var dto = new AddPaymentDto { OrderId = order.Id, CurrencyId = 1 };
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.CreatePayment(dto, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Order with id '{order.Id}' has alredy been paid");
        }

        [Fact]
        public void should_create_payment()
        {
            var order = CreateDefaultOrder();
            var currencyRate = AddDefaultCurrencyRate();
            var dto = new AddPaymentDto { OrderId = order.Id, CurrencyId = currencyRate.CurrencyId };
            var paymentHandler = CreatePaymentHandler();

            paymentHandler.CreatePayment(dto, order);

            _paymentRepository.Verify(p => p.AddPayment(It.Is<Domain.Model.Payment>(p => p.OrderId == dto.OrderId)),
                times: Times.Once);
        }

        [Fact]
        public void given_null_vm_when_pay_issued_payment_should_throw_an_exception()
        {
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.PayIssuedPayment(null, null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"{typeof(PaymentVm).Name} cannot be null");
        }

        [Fact]
        public void given_null_order_when_pay_issued_payment_should_throw_an_exception()
        {
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.PayIssuedPayment(new PaymentVm(), null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"{typeof(Order).Name} cannot be null");
        }

        [Fact]
        public void given_paid_order_when_pay_issued_payment_should_throw_an_exception()
        {
            var order = new Order() { IsPaid = true };
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.PayIssuedPayment(new PaymentVm(), order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Order with id '{order.Id}' has alredy been paid");
        }

        [Fact]
        public void given_paid_payment_when_pay_issued_payment_should_throw_an_exception()
        {
            var vm = new PaymentVm { State = PaymentState.Paid };
            var order = new Order();
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.PayIssuedPayment(vm, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Payment with id '{vm.Id}' was already paid");
        }

        [Fact]
        public void given_not_existing_payment_when_pay_issued_payment_should_throw_an_exception()
        {
            var vm = new PaymentVm();
            var order = new Order();
            var paymentHandler = CreatePaymentHandler();

            Action action = () => paymentHandler.PayIssuedPayment(vm, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Payment with id '{vm.Id}' was not found");
        }

        [Fact]
        public void should_pay_issued_payment()
        {
            var currencyRate = AddDefaultCurrencyRate();
            var order = CreateDefaultOrder();
            var paymentHandler = CreatePaymentHandler();
            var payment = AddDefaultPayment();
            payment.CurrencyId = currencyRate.CurrencyId;
            var paymentVm = new PaymentVm
            {
                Id = payment.Id,
                State = payment.State,
                OrderId = payment.Id,
                Cost = payment.Cost,
                CurrencyId = payment.CurrencyId,
                DateOfOrderPayment = payment.DateOfOrderPayment,
            };

            paymentHandler.PayIssuedPayment(paymentVm, order);

            _paymentRepository.Verify(p => p.Update(payment), times: Times.Once);
        }

        [Fact]
        public void given_null_order_when_handle_payment_changes_on_order_should_throw_an_exception()
        {
            var dto = new PaymentInfoDto();
            var paymentHandler = CreatePaymentHandler();

            var action = () => paymentHandler.HandlePaymentChangesOnOrder(dto, null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"{typeof(Order).Name} cannot be null");
        }

        [Fact]
        public void given_order_with_payment_id_and_dto_with_different_id_when_handle_payment_changes_on_order_should_not_allow_override_payment_and_throw_an_exception()
        {
            var dto = new PaymentInfoDto() { Id = 10 };
            var order = CreateDefaultOrder();
            order.IsPaid = true;
            order.PaymentId = 100;
            var paymentHandler = CreatePaymentHandler();

            var action = () => paymentHandler.HandlePaymentChangesOnOrder(dto, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Overriding payment id on order is not allowed");
        }

        [Fact]
        public void given_order_with_payment_id_and_dto_with_same_id_should_do_nothing()
        {
            var dto = new PaymentInfoDto();
            var order = CreateDefaultOrder();
            order.IsPaid = true;
            order.PaymentId = 100;
            dto.Id = order.PaymentId;
            var paymentHandler = CreatePaymentHandler();

            paymentHandler.HandlePaymentChangesOnOrder(dto, order);

            _paymentRepository.Verify(p => p.AddPayment(It.IsAny<Domain.Model.Payment>()), times: Times.Never);
            _paymentRepository.Verify(p => p.Delete(It.IsAny<int>()), times: Times.Never);
        }

        [Fact]
        public void given_null_dto_and_order_without_payment_id_should_do_nothing()
        {
            var order = CreateDefaultOrder();
            var paymentHandler = CreatePaymentHandler();

            paymentHandler.HandlePaymentChangesOnOrder(null, order);

            _paymentRepository.Verify(p => p.AddPayment(It.IsAny<Domain.Model.Payment>()), times: Times.Never);
            _paymentRepository.Verify(p => p.Delete(It.IsAny<int>()), times: Times.Never);
        }

        [Fact]
        public void given_order_with_payment_id_and_dto_without_id_should_not_allow_to_add_new_and_throw_an_exception()
        {
            var dto = new PaymentInfoDto();
            var order = CreateDefaultOrder();
            order.PaymentId = 100;
            order.IsPaid = true;
            var paymentHandler = CreatePaymentHandler();

            var action = () => paymentHandler.HandlePaymentChangesOnOrder(dto, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Cannot pay for paid order with id '{order.Id}'");
        }

        [Fact]
        public void given_order_without_payment_id_and_dto_with_id_should_not_allow_to_replace_payment_and_throw_an_exception()
        {
            var dto = new PaymentInfoDto() { Id = 100 };
            var order = CreateDefaultOrder();
            var paymentHandler = CreatePaymentHandler();

            var action = () => paymentHandler.HandlePaymentChangesOnOrder(dto, order);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Overriding payment id on order is not allowed");
        }

        [Fact]
        public void given_null_dto_and_order_with_payment_id_should_delete_payment()
        {
            var order = CreateDefaultOrder();
            var paymentId = 100;
            order.IsPaid = true;
            order.PaymentId = paymentId;
            var paymentHandler = CreatePaymentHandler();

            paymentHandler.HandlePaymentChangesOnOrder(null, order);

            _paymentRepository.Verify(p => p.Delete(paymentId), times: Times.Once);
            order.IsPaid.Should().BeFalse();
            order.Payment.Should().BeNull();
        }

        [Fact]
        public void given_valid_dto_and_order_without_payment_id_should_create_payment()
        {
            var dto = new PaymentInfoDto();
            var order = CreateDefaultOrder();
            var rate = AddDefaultCurrencyRate();
            dto.CurrencyId = rate.CurrencyId;
            var paymentHandler = CreatePaymentHandler();

            paymentHandler.HandlePaymentChangesOnOrder(dto, order);

            _paymentRepository.Verify(p => p.AddPayment(It.Is<Domain.Model.Payment>(p => p.OrderId == order.Id)), times: Times.Once);
        }

        private static Domain.Model.Order CreateDefaultOrder()
        {
            var order = new Domain.Model.Order
            {
                Id = new Random().Next(1, 9999),
                Number = "1234557567",
                Cost = new decimal(100),
                Ordered = DateTime.Now,
                IsPaid = false,
                IsDelivered = false,
                Delivered = null,
                CustomerId = 1,
                CurrencyId = 1,
                UserId = Guid.NewGuid().ToString(),
                OrderItems = new List<Domain.Model.OrderItem>(),
                Currency = new Domain.Model.Currency() { Id = 1 },
                Customer = new Domain.Model.Customer() { Id = 1 }
            };
            order.User = new ApplicationUser { Id = order.UserId };
            return order;
        }

        private Domain.Model.Payment AddDefaultPayment()
        {
            return AddPayment(CreateDefaultPayment());
        }

        private Domain.Model.Payment AddPayment(Domain.Model.Payment payment)
        {
            _paymentRepository.Setup(p => p.GetById(payment.Id)).Returns(payment);
            _paymentRepository.Setup(p => p.GetPaymentByOrderId(payment.OrderId)).Returns(payment);
            return payment;
        }

        private CurrencyRateDto AddDefaultCurrencyRate()
        {
            var dto = new CurrencyRateDto
            {
                Id = 1,
                CurrencyId = 1,
                CurrencyDate = DateTime.Now,
                Rate = 1M
            };
            _currencyRateService.Setup(cr => cr.GetLatestRate(dto.CurrencyId)).Returns(dto);
            return dto;
        }

        private static Domain.Model.Payment CreateDefaultPayment()
        {
            return CreatePayment(new Random().Next(1, 9999), 1, 1);
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
    }
}
