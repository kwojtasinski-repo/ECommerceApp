using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.Sales.Fulfillment
{
    public class RefundServiceTests : BcBaseTest<IRefundService>
    {
        public RefundServiceTests(ITestOutputHelper output) : base(output) { }

        // ── RequestRefundAsync ───────────────────────────────────────────

        [Fact]
        public async Task RequestRefundAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var dto = new RequestRefundDto(
                OrderId: int.MaxValue,
                Reason: "Damaged goods",
                OnWarranty: false,
                Items: new List<RequestRefundItemDto> { new(ProductId: 1, Quantity: 1) },
                UserId: "test-user");

            var result = await _service.RequestRefundAsync(dto);

            result.ShouldBe(RefundRequestResult.OrderNotFound);
        }

        // ── ApproveRefundAsync ───────────────────────────────────────────

        [Fact]
        public async Task ApproveRefundAsync_NonExistentRefund_ShouldReturnRefundNotFound()
        {
            var result = await _service.ApproveRefundAsync(int.MaxValue);

            result.ShouldBe(RefundOperationResult.RefundNotFound);
        }

        // ── RejectRefundAsync ────────────────────────────────────────────

        [Fact]
        public async Task RejectRefundAsync_NonExistentRefund_ShouldReturnRefundNotFound()
        {
            var result = await _service.RejectRefundAsync(int.MaxValue);

            result.ShouldBe(RefundOperationResult.RefundNotFound);
        }

        // ── GetRefundAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetRefundAsync_NonExistentRefund_ShouldReturnNull()
        {
            var result = await _service.GetRefundAsync(int.MaxValue);

            result.ShouldBeNull();
        }

        // ── GetRefundsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetRefundsAsync_EmptyDatabase_ShouldReturnEmptyPage()
        {
            var result = await _service.GetRefundsAsync(pageSize: 10, pageNo: 1, search: null);

            result.ShouldNotBeNull();
            result.PageSize.ShouldBe(10);
            result.CurrentPage.ShouldBe(1);
            result.TotalCount.ShouldBe(0);
            result.Refunds.ShouldNotBeNull();
            result.Refunds.ShouldBeEmpty();
        }
    }
}
