using ECommerceApp.Application.Services.Refunds;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class RefundServiceTests : BaseTest<IRefundService>
    {
        [Fact]
        public void given_valid_id_should_return_refund_details()
        {
            var id = 1;

            var refund = _service.GetRefundDetails(id);

            refund.ShouldNotBeNull();
            refund.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_refund_details()
        {
            var id = 13465467;

            var refund = _service.GetRefundDetails(id);

            refund.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_refund()
        {
            var id = 1;

            var refund = _service.GetRefundById(id);

            refund.ShouldNotBeNull();
            refund.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_refund()
        {
            var id = 1346537;

            var refund = _service.GetRefundById(id);

            refund.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_should_return_refunds()
        {
            var refunds = _service.GetRefunds(r => true);

            refunds.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_page_size_page_no_search_string_should_return_refunds()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var refunds = _service.GetRefunds(pageSize, pageNo, searchString);

            refunds.Refunds.Count.ShouldBeGreaterThan(0);
            refunds.Count.ShouldBeGreaterThan(0);
            refunds.PageSize.ShouldBe(pageSize);
            refunds.CurrentPage.ShouldBe(pageNo);
            refunds.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_id_should_delete_refund()
        {
            var refund = new RefundVm { Id = 0, Accepted = true, CustomerId = 1, OrderId = 5, OnWarranty = true, Reason = "reason", RefundDate = DateTime.Now };
            var id = _service.AddRefund(refund);

            _service.DeleteRefund(id);

            var refundDeleted = _service.Get(id);
            refundDeleted.ShouldBeNull();
        }
    }
}
