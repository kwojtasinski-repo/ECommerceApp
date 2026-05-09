using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using ECommerceApp.Application.Supporting.Communication.Handlers;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FulfillmentMessages = ECommerceApp.Application.Sales.Fulfillment.Messages;

namespace ECommerceApp.UnitTests.Supporting.Communication
{
    public class OrderPlacedEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();

        private OrderPlacedEmailHandler CreateHandler()
            => new(_emails.Object, _emailResolver.Object);

        private static OrderPlaced Message(int orderId = 1, string userId = "user-1", decimal total = 99.99m)
            => new(orderId, new List<OrderPlacedItem>(), userId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow, total, 1);

        [Fact]
        public async Task HandleAsync_SendsEmailToOrderOwner()
        {
            _emailResolver.Setup(r => r.GetEmailForUserAsync("user-5", It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user5@test.com");

            await CreateHandler().HandleAsync(Message(orderId: 5, userId: "user-5"), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user5@test.com"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SubjectContainsOrderId()
        {
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(orderId: 42), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Subject.Contains("42")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_BodyContainsTotalAmount()
        {
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(orderId: 1, total: 149.50m), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Body.Contains("149")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionWithOrderId()
        {
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(orderId: 7), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Actions != null && t.Actions.Count == 1 && t.Actions[0].Url.Contains("7")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class PaymentConfirmedEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();

        private PaymentConfirmedEmailHandler CreateHandler()
            => new(_emails.Object, _resolver.Object, _emailResolver.Object);

        private static PaymentConfirmed Message(int paymentId = 1, int orderId = 10)
            => new(paymentId, orderId, new List<PaymentConfirmedItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");
            _emailResolver.Setup(r => r.GetEmailForUserAsync("user-10", It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user10@test.com");

            await CreateHandler().HandleAsync(Message(orderId: 10), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionWithPaymentId()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(paymentId: 99), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Actions != null && t.Actions[0].Url.Contains("99")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class RefundApprovedEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();

        private RefundApprovedEmailHandler CreateHandler()
            => new(_emails.Object, _resolver.Object, _emailResolver.Object);

        private static FulfillmentMessages.RefundApproved Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, new List<FulfillmentMessages.RefundApprovedItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");
            _emailResolver.Setup(r => r.GetEmailForUserAsync("user-10", It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user10@test.com");

            await CreateHandler().HandleAsync(Message(refundId: 3, orderId: 10), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("3")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionWithRefundId()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(refundId: 55), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Actions != null && t.Actions[0].Url.Contains("55")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class OrderCancelledEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();

        private OrderCancelledEmailHandler CreateHandler()
            => new(_emails.Object, _resolver.Object, _emailResolver.Object);

        private static OrderCancelled Message(int orderId = 1)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(5, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-5");
            _emailResolver.Setup(r => r.GetEmailForUserAsync("user-5", It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user5@test.com");

            await CreateHandler().HandleAsync(Message(orderId: 5), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user5@test.com" && t.Subject.Contains("5")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class PaymentExpiredEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();

        private PaymentExpiredEmailHandler CreateHandler()
            => new(_emails.Object, _resolver.Object, _emailResolver.Object);

        private static PaymentExpired Message(int paymentId = 1, int orderId = 10)
            => new(paymentId, orderId, DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");
            _emailResolver.Setup(r => r.GetEmailForUserAsync("user-10", It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user10@test.com");

            await CreateHandler().HandleAsync(Message(paymentId: 3, orderId: 10), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_BodyMentionsPaymentExpiry()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(paymentId: 7), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Body.Contains("7") && t.Body.Contains("anulowane")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class RefundRejectedEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();

        private RefundRejectedEmailHandler CreateHandler()
            => new(_emails.Object, _resolver.Object, _emailResolver.Object);

        private static FulfillmentMessages.RefundRejected Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");
            _emailResolver.Setup(r => r.GetEmailForUserAsync("user-10", It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user10@test.com");

            await CreateHandler().HandleAsync(Message(refundId: 4, orderId: 10), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("4")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionLinkingToOrder()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-1");
            _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("user@test.com");

            await CreateHandler().HandleAsync(Message(refundId: 1, orderId: 20), TestContext.Current.CancellationToken);

            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Actions != null && t.Actions[0].Url.Contains("20")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
