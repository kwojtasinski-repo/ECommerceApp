using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using ECommerceApp.Application.Supporting.Communication.Handlers;
using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private void SetupEmailRecipient(string userId, string email)
            => _emailResolver.Setup(r => r.GetEmailForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(email);

        private void SetupAnyEmail(string email)
            => _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(email);

        private OrderPlacedEmailHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(_emails.Object, _emailResolver.Object, _processedMessageGuard.Object);
        }

        private static OrderPlaced Message(int orderId = 1, string userId = "user-1", decimal total = 99.99m)
            => new(orderId, new List<OrderPlacedItem>(), userId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow, total, 1);

        [Fact]
        public async Task HandleAsync_SendsEmailToOrderOwner()
        {
            // Arrange
            SetupEmailRecipient("user-5", "user5@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(orderId: 5, userId: "user-5"), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user5@test.com"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            // Arrange
            SetupAnyEmail(null);

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SubjectContainsOrderId()
        {
            // Arrange
            SetupAnyEmail("user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(orderId: 42), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Subject.Contains("42")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_BodyContainsTotalAmount()
        {
            // Arrange
            SetupAnyEmail("user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(orderId: 1, total: 149.50m), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Body.Contains("149")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionWithOrderId()
        {
            // Arrange
            SetupAnyEmail("user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(orderId: 7), 1, TestContext.Current.CancellationToken);

            // Assert
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
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private void SetupUserEmail(string userId, string email)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(userId);
            _emailResolver.Setup(r => r.GetEmailForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        }

        private void SetupUserNotResolved()
            => _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        private void SetupAnyUserEmail(string email)
            => _emailResolver.Setup(r => r.GetEmailForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(email);

        private PaymentConfirmedEmailHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(
                _emails.Object,
                _resolver.Object,
                _emailResolver.Object,
                _processedMessageGuard.Object);
        }

        private static PaymentConfirmed Message(int paymentId = 1, int orderId = 10)
            => new(paymentId, orderId, new List<PaymentConfirmedItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            // Arrange
            SetupUserEmail("user-10", "user10@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(orderId: 10), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserNotResolved();

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserEmail("user-1", null);

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionWithPaymentId()
        {
            // Arrange
            SetupUserEmail("user-1", "user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(paymentId: 99), 1, TestContext.Current.CancellationToken);

            // Assert
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
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private void SetupUserEmail(string userId, string email)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(userId);
            _emailResolver.Setup(r => r.GetEmailForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        }

        private void SetupUserNotResolved()
            => _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        private RefundApprovedEmailHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(
                _emails.Object,
                _resolver.Object,
                _emailResolver.Object,
                _processedMessageGuard.Object,
                _outboxWriter.Object);
        }

        private static FulfillmentMessages.RefundApproved Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, new List<FulfillmentMessages.RefundApprovedItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            // Arrange
            SetupUserEmail("user-10", "user10@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(refundId: 3, orderId: 10), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("3")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserNotResolved();

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserEmail("user-1", null);

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionWithRefundId()
        {
            // Arrange
            SetupUserEmail("user-1", "user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(refundId: 55), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Actions != null && t.Actions[0].Url.Contains("55")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailSent_ShouldPublishCustomerNotified()
        {
            // Arrange
            SetupUserEmail("user-1", "user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(refundId: 55), 1, TestContext.Current.CancellationToken);

            // Assert
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<RefundCustomerNotified>(m => m.RefundId == 55),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class OrderCancelledEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private void SetupUserEmail(string userId, string email)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(userId);
            _emailResolver.Setup(r => r.GetEmailForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        }

        private void SetupUserNotResolved()
            => _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        private OrderCancelledEmailHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(_emails.Object, _resolver.Object, _emailResolver.Object, _processedMessageGuard.Object);
        }

        private static OrderCancelled Message(int orderId = 1)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            // Arrange
            SetupUserEmail("user-5", "user5@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(orderId: 5), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user5@test.com" && t.Subject.Contains("5")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserNotResolved();

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserEmail("user-1", null);

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class PaymentExpiredEmailHandlerTests
    {
        private readonly Mock<IEmailService> _emails = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IUserEmailResolver> _emailResolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private void SetupUserEmail(string userId, string email)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(userId);
            _emailResolver.Setup(r => r.GetEmailForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        }

        private void SetupUserNotResolved()
            => _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        private PaymentExpiredEmailHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(_emails.Object, _resolver.Object, _emailResolver.Object, NullLogger<PaymentExpiredEmailHandler>.Instance, _processedMessageGuard.Object);
        }

        private static PaymentExpired Message(int paymentId = 1, int orderId = 10)
            => new(paymentId, orderId, DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            // Arrange
            SetupUserEmail("user-10", "user10@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(paymentId: 3, orderId: 10), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserNotResolved();

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserEmail("user-1", null);

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_BodyMentionsPaymentExpiry()
        {
            // Arrange
            SetupUserEmail("user-1", "user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(paymentId: 7), 1, TestContext.Current.CancellationToken);

            // Assert
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
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private void SetupUserEmail(string userId, string email)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(userId);
            _emailResolver.Setup(r => r.GetEmailForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        }

        private void SetupUserNotResolved()
            => _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        private RefundRejectedEmailHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(_emails.Object, _resolver.Object, _emailResolver.Object, _processedMessageGuard.Object);
        }

        private static FulfillmentMessages.RefundRejected Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_SendsEmail()
        {
            // Arrange
            SetupUserEmail("user-10", "user10@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(refundId: 4, orderId: 10), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.To == "user10@test.com" && t.Subject.Contains("4")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserNotResolved();

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenEmailNotResolved_SkipsEmail()
        {
            // Arrange
            SetupUserEmail("user-1", null);

            // Act
            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_IncludesActionLinkingToOrder()
        {
            // Arrange
            SetupUserEmail("user-1", "user@test.com");

            // Act
            await CreateHandler().HandleAsync(Message(refundId: 1, orderId: 20), 1, TestContext.Current.CancellationToken);

            // Assert
            _emails.Verify(e => e.SendAsync(
                It.Is<EmailTemplate>(t => t.Actions != null && t.Actions[0].Url.Contains("20")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
