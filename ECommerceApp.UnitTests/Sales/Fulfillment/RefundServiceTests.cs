using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Domain.Sales.Fulfillment;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Fulfillment
{
    public class RefundServiceTests
    {
        private readonly Mock<IRefundRepository> _refunds;
        private readonly Mock<IModuleClient> _moduleClient;
        private readonly Mock<IMessageBroker> _broker;

        public RefundServiceTests()
        {
            _refunds = new Mock<IRefundRepository>();
            _moduleClient = new Mock<IModuleClient>();
            _broker = new Mock<IMessageBroker>();
        }

        private IRefundService CreateService()
            => new RefundService(_refunds.Object, _moduleClient.Object, _broker.Object);

        private static Refund CreateRequestedRefund(int id = 1, int orderId = 99)
        {
            var items = new[] { RefundItem.Create(10, 2), RefundItem.Create(20, 1) };
            var refund = Refund.Create(orderId, "Defective", true, items, "user-1");
            typeof(Refund).GetProperty(nameof(Refund.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(refund, new object[] { new RefundId(id) });
            return refund;
        }

        private static RequestRefundDto CreateDto(int orderId = 99)
            => new(orderId, "Defective", true, new List<RequestRefundItemDto>
            {
                new(10, 2),
                new(20, 1)
            }, UserId: "user-1");

        // ── RequestRefundAsync ────────────────────────────────────────────────

        [Fact]
        public async Task RequestRefundAsync_OrderNotFound_ShouldReturnOrderNotFound()
        {
            _moduleClient.Setup(x => x.SendAsync(It.IsAny<OrderExistsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await CreateService().RequestRefundAsync(CreateDto(), TestContext.Current.CancellationToken);

            result.Should().Be(RefundRequestResult.OrderNotFound);
            _refunds.Verify(r => r.AddAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RequestRefundAsync_RefundAlreadyExists_ShouldReturnRefundAlreadyExists()
        {
            _moduleClient.Setup(x => x.SendAsync(It.IsAny<OrderExistsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _refunds.Setup(x => x.FindActiveByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(CreateRequestedRefund());

            var result = await CreateService().RequestRefundAsync(CreateDto(), TestContext.Current.CancellationToken);

            result.Should().Be(RefundRequestResult.RefundAlreadyExists);
            _refunds.Verify(r => r.AddAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RequestRefundAsync_HappyPath_ShouldCreateAndPersistRefund()
        {
            _moduleClient.Setup(x => x.SendAsync(It.IsAny<OrderExistsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _refunds.Setup(x => x.FindActiveByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Refund)null);

            var result = await CreateService().RequestRefundAsync(CreateDto(), TestContext.Current.CancellationToken);

            result.Should().Be(RefundRequestResult.Requested);
            _refunds.Verify(r => r.AddAsync(It.Is<Refund>(ref_ =>
                ref_.OrderId == 99 &&
                ref_.Reason == "Defective" &&
                ref_.OnWarranty == true &&
                ref_.Status == RefundStatus.Requested &&
                ref_.Items.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── ApproveRefundAsync ────────────────────────────────────────────────

        [Fact]
        public async Task ApproveRefundAsync_RefundNotFound_ShouldReturnRefundNotFound()
        {
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Refund)null);

            var result = await CreateService().ApproveRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.RefundNotFound);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApproveRefundAsync_AlreadyApproved_ShouldReturnAlreadyProcessed()
        {
            var refund = CreateRequestedRefund();
            refund.Approve();
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var result = await CreateService().ApproveRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.AlreadyProcessed);
            _refunds.Verify(r => r.UpdateAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApproveRefundAsync_HappyPath_ShouldApproveUpdateAndPublishRefundApproved()
        {
            var refund = CreateRequestedRefund(id: 5, orderId: 99);
            _refunds.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var result = await CreateService().ApproveRefundAsync(5, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.Success);
            refund.Status.Should().Be(RefundStatus.Approved);
            _refunds.Verify(r => r.UpdateAsync(refund, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(RefundApproved) &&
                ((RefundApproved)m[0]).RefundId == 5 &&
                ((RefundApproved)m[0]).OrderId == 99 &&
                ((RefundApproved)m[0]).Items.Count == 2 &&
                ((RefundApproved)m[0]).Items[0].ProductId == 10 &&
                ((RefundApproved)m[0]).Items[0].Quantity == 2 &&
                ((RefundApproved)m[0]).Items[1].ProductId == 20 &&
                ((RefundApproved)m[0]).Items[1].Quantity == 1)), Times.Once);
        }

        // ── RejectRefundAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task RejectRefundAsync_RefundNotFound_ShouldReturnRefundNotFound()
        {
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Refund)null);

            var result = await CreateService().RejectRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.RefundNotFound);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task RejectRefundAsync_AlreadyRejected_ShouldReturnAlreadyProcessed()
        {
            var refund = CreateRequestedRefund();
            refund.Reject();
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var result = await CreateService().RejectRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.AlreadyProcessed);
            _refunds.Verify(r => r.UpdateAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task RejectRefundAsync_AlreadyApproved_ShouldReturnAlreadyProcessed()
        {
            var refund = CreateRequestedRefund();
            refund.Approve();
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var result = await CreateService().RejectRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.AlreadyProcessed);
            _refunds.Verify(r => r.UpdateAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task RejectRefundAsync_HappyPath_ShouldRejectUpdateAndPublishRefundRejected()
        {
            var refund = CreateRequestedRefund(id: 5, orderId: 99);
            _refunds.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var result = await CreateService().RejectRefundAsync(5, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.Success);
            refund.Status.Should().Be(RefundStatus.Rejected);
            _refunds.Verify(r => r.UpdateAsync(refund, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(RefundRejected) &&
                ((RefundRejected)m[0]).RefundId == 5 &&
                ((RefundRejected)m[0]).OrderId == 99)), Times.Once);
        }

        // ── GetRefundAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task GetRefundAsync_NotFound_ShouldReturnNull()
        {
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Refund)null);

            var result = await CreateService().GetRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRefundAsync_Found_ShouldReturnMappedVm()
        {
            var refund = CreateRequestedRefund(id: 5, orderId: 99);
            _refunds.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var result = await CreateService().GetRefundAsync(5, TestContext.Current.CancellationToken);

            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.OrderId.Should().Be(99);
            result.Reason.Should().Be("Defective");
            result.OnWarranty.Should().BeTrue();
            result.Status.Should().Be("Requested");
            result.Items.Should().HaveCount(2);
        }

        // ── GetRefundsAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetRefundsAsync_ShouldReturnPagedList()
        {
            var refund = CreateRequestedRefund(id: 1, orderId: 99);
            _refunds.Setup(x => x.GetPagedAsync(10, 1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Refund> { refund });
            _refunds.Setup(x => x.GetCountAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var result = await CreateService().GetRefundsAsync(10, 1, null, TestContext.Current.CancellationToken);

            result.Refunds.Should().HaveCount(1);
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(1);
        }
    }
}
