using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Linq;
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
            SetHttpContextUserId(PROPER_CUSTOMER_ID);

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
            SetHttpContextUserId(PROPER_CUSTOMER_ID);

            var payment = _service.GetPaymentDetails(id);

            payment.ShouldNotBeNull();
            payment.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_user_id_should_return_null_payment_details()
        {
            SetHttpContextUserId("");
            var id = 1;

            var payment = _service.GetPaymentDetails(id);

            payment.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_should_return_list_payment()
        {
            var payments = _service.GetPayments();

            payments.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_expression_and_user_id_should_return_list_payment()
        {
            var userId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var payments = _service.GetUserPayments(userId);

            payments.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_order_id_should_init_payment()
        {
            var orderId = 1;

            var payment = _service.InitPayment(orderId);

            payment.Number.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public void given_valid_payment_id_should_delete()
        {
            var payment = new AddPaymentDto() { CurrencyId = 1, OrderId = 1 };
            var id = _service.AddPayment(payment);

            _service.DeletePayment(id);

            var paymentDeleted = _service.GetPaymentById(id);
            paymentDeleted.ShouldBeNull();
        }
    }
}
