using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.CouponUsed;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Linq;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class CouponUsedServiceTests : BaseTest<ICouponUsedService>
    {
        [Fact]
        public void given_valid_coupon_used_should_add()
        {
            var couponUsed = CreateCouponUsed(0);

            var id = _service.AddCouponUsed(couponUsed);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_coupon_should_throw_an_exception()
        {
            var couponUsed = CreateCouponUsed(124);

            var exception = Should.Throw<BusinessException>(() => _service.AddCouponUsed(couponUsed));

            exception.Message.ShouldBe("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_id_should_return_coupon_used()
        {
            var couponUsed = CreateCouponUsed(0);
            var id = _service.AddCouponUsed(couponUsed);

            var coupon = _service.GetCouponUsed(id);

            coupon.ShouldNotBeNull();
        }

        [Fact]
        public void given_invalid_id_should_return_null_coupon_used()
        {
            var id = 124534;

            var coupon = _service.GetCouponUsed(id);

            coupon.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_coupon_used_detail()
        {
            var couponUsed = CreateCouponUsed(0);
            var id = _service.AddCouponUsed(couponUsed);

            var coupon = _service.GetCouponUsedDetail(id);

            coupon.ShouldNotBeNull();
        }

        [Fact]
        public void given_invalid_id_should_return_null_coupon_used_detail()
        {
            var id = 124534;

            var coupon = _service.GetCouponUsedDetail(id);

            coupon.ShouldBeNull();
        }

        [Fact]
        public void given_valid_coupons_in_db_should_return_list_coupons()
        {
            _service.AddCouponUsed(CreateCouponUsed(0));

            var coupons = _service.GetAllCouponsUsed();

            coupons.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_expression_should_return_list_coupon_used()
        {
            _service.AddCouponUsed(CreateCouponUsed(0));

            var coupons = _service.GetAllCouponsUsed(cu => true);

            coupons.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_list_coupon_used()
        {
            _service.AddCouponUsed(CreateCouponUsed(0));

            var coupons = _service.GetAllCouponsUsed(cu => cu.Id == 8998908);

            coupons.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_page_size_page_number_and_search_string_should_return_list_coupon_used()
        {
            var pageSize = 20;
            var pageNumber = 1;
            var searchString = "";

            var coupons = _service.GetAllCouponsUsed(pageSize, pageNumber, searchString);

            coupons.Count.ShouldBe(0);
            coupons.CouponsUsed.Count.ShouldBe(0);
            coupons.CurrentPage.ShouldBe(pageNumber);
            coupons.PageSize.ShouldBe(pageSize);
            coupons.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_invalid_search_string_should_return_empty_list_coupon_used()
        {
            var pageSize = 20;
            var pageNumber = 1;
            var searchString = "asfat23525sd";

            var coupons = _service.GetAllCouponsUsed(pageSize, pageNumber, searchString);

            coupons.Count.ShouldBe(0);
            coupons.CouponsUsed.Count.ShouldBe(0);
            coupons.CurrentPage.ShouldBe(pageNumber);
            coupons.PageSize.ShouldBe(pageSize);
            coupons.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_coupon_should_update()
        {
            var couponUsed = CreateCouponUsed(0);
            var id = _service.AddCouponUsed(couponUsed);
            couponUsed = _service.Get(id);
            var orderId = 2;
            couponUsed.OrderId = orderId;

            _service.UpdateCouponUsed(couponUsed);

            var coupounUsedUpdated = _service.Get(id);
            coupounUsedUpdated.ShouldNotBeNull();
            coupounUsedUpdated.OrderId.ShouldBe(orderId);
        }

        [Fact]
        public void given_valid_id_should_delete_coupon_used()
        {
            var couponUsed = CreateCouponUsed(0);
            var id = _service.AddCouponUsed(couponUsed);

            _service.DeleteCouponUsed(id);

            var coupounUsedDeleted = _service.Get(id);
            coupounUsedDeleted.ShouldBeNull();
        }

        private CouponUsedVm CreateCouponUsed(int id)
        {
            var couponUsed = new CouponUsedVm
            {
                Id = id,
                CouponId = 1,
                OrderId = 1
            };
            return couponUsed;
        }
    }
}
