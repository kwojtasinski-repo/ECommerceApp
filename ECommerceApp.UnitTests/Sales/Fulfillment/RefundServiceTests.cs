using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment;
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
        private readonly Mock<IFulfillmentUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxWriter> _outboxWriter;

        public RefundServiceTests()
        {
            _refunds = new Mock<IRefundRepository>();
            _moduleClient = new Mock<IModuleClient>();
            _unitOfWork = new Mock<IFulfillmentUnitOfWork>();
            _outboxWriter = new Mock<IOutboxWriter>();
        }

        private IRefundService CreateService(Mock<IOutboxTransaction> txMock = null)
        {
            txMock ??= new Mock<IOutboxTransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            return new RefundService(_refunds.Object, _moduleClient.Object, _unitOfWork.Object, _outboxWriter.Object);
        }

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
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ApproveRefundAsync_HappyPath_ShouldApproveUpdateAndPublishRefundApproved()
        {
            var refund = CreateRequestedRefund(id: 5, orderId: 99);
            _refunds.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var txMock = new Mock<IOutboxTransaction>();
            var result = await CreateService(txMock).ApproveRefundAsync(5, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.Success);
            refund.Status.Should().Be(RefundStatus.Approved);
            _refunds.Verify(r => r.UpdateAsync(refund, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<RefundApproved>(m =>
                    m.RefundId == 5 &&
                    m.OrderId == 99 &&
                    m.Items.Count == 2 &&
                    m.Items[0].ProductId == 10 &&
                    m.Items[0].Quantity == 2 &&
                    m.Items[1].ProductId == 20 &&
                    m.Items[1].Quantity == 1),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── RejectRefundAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task RejectRefundAsync_RefundNotFound_ShouldReturnRefundNotFound()
        {
            _refunds.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Refund)null);

            var result = await CreateService().RejectRefundAsync(1, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.RefundNotFound);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RejectRefundAsync_HappyPath_ShouldRejectUpdateAndPublishRefundRejected()
        {
            var refund = CreateRequestedRefund(id: 5, orderId: 99);
            _refunds.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

            var txMock = new Mock<IOutboxTransaction>();
            var result = await CreateService(txMock).RejectRefundAsync(5, TestContext.Current.CancellationToken);

            result.Should().Be(RefundOperationResult.Success);
            refund.Status.Should().Be(RefundStatus.Rejected);
            _refunds.Verify(r => r.UpdateAsync(refund, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<RefundRejected>(m =>
                    m.RefundId == 5 &&
                    m.OrderId == 99),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
