using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class PaymentServiceTests : BaseTest<IPaymentService>
    {
        [Fact]
        public void given_valid_id_should_return_payment()
        {
            var id = 1;

            var payment = _service.GetPaymentById(id);

            payment.ShouldNotBeNull();
            payment.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_payment()
        {
            var id = 165473;

            var payment = _service.GetPaymentById(id);

            payment.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_payment_details()
        {
            var id = 1;

            var payment = _service.GetPaymentDetails(id);

            payment.ShouldNotBeNull();
            payment.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_payment_details()
        {
            var id = 165473;

            var payment = _service.GetPaymentDetails(id);

            payment.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_and_user_id_should_return_payment_details()
        {
            var id = 1;
            var userId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var payment = _service.GetPaymentDetails(id, userId);

            payment.ShouldNotBeNull();
            payment.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_user_id_should_return_null_payment_details()
        {
            var id = 1;

            var payment = _service.GetPaymentDetails(id, "");

            payment.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_should_return_list_payment()
        {
            var payments = _service.GetPayments(p => true);

            payments.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_expression_and_user_id_should_return_list_payment()
        {
            var userId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var payments = _service.GetPaymentsForUser(p => true, userId);

            payments.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_order_id_should_init_payment()
        {
            var orderId = 1;

            var payment = _service.InitPayment(orderId);

            payment.Number.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_payment_id_should_delete()
        {
            var payment = new PaymentVm() { Id = 0, DateOfOrderPayment = DateTime.Now, Number = 1235, CurrencyId = 1, CustomerId = 1, OrderCost = 100M, OrderId = 1, OrderNumber = 1242 };
            var id = _service.AddPayment(payment);

            _service.DeletePayment(id);

            var paymentDeleted = _service.GetPaymentById(id);
            paymentDeleted.ShouldBeNull();
        }
    }
}
