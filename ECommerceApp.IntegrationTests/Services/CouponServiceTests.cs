using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class CouponServiceTests : BaseTest<ICouponService>
    {
        [Fact]
        public void given_valid_id_should_return_coupon()
        {
            var id = 1;
            var code = "AGEWEDSGFEW";

            var coupon = _service.GetCoupon(id);

            coupon.ShouldNotBeNull();
            coupon.Id.ShouldBe(id);
            coupon.Code.ShouldBe(code);
        }

        [Fact]
        public void given_invalid_id_should_return_null()
        {
            var id = 24123;

            var coupon = _service.GetCoupon(id);

            coupon.ShouldBeNull();
        }

        [Fact]
        public void given_coupon_code_should_return_coupon()
        {
            var id = 1;
            var code = "AGEWEDSGFEW";

            var coupon = _service.GetCouponByCode(code);

            coupon.ShouldNotBeNull();
            coupon.Id.ShouldBe(id);
            coupon.Code.ShouldBe(code);
        }

        [Fact]
        public void given_invalid_coupon_code_should_return_null()
        {
            var code = "asfafs";

            var coupon = _service.GetCouponByCode(code);

            coupon.ShouldBeNull();
        }

        [Fact]
        public void given_coupon_id_should_return_coupon_details()
        {
            var id = 1;
            var code = "AGEWEDSGFEW";

            var coupon = _service.GetCouponDetail(id);

            coupon.ShouldNotBeNull();
            coupon.Id.ShouldBe(id);
            coupon.Code.ShouldBe(code);
        }

        [Fact]
        public void given_invalid_coupon_id_code_should_return_null()
        {
            var id = 1234567;

            var coupon = _service.GetCouponDetail(id);

            coupon.ShouldBeNull();
        }

        [Fact]
        public void given_valid_code_should_return_id()
        {
            var code = "AGEWEDSGFEW";

            var id = _service.CheckPromoCode(code);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_code_shouldnt_return_id()
        {
            var code = "asf12W";

            var id = _service.CheckPromoCode(code);

            id.ShouldBe(0);
        }

        [Fact]
        public void given_valid_expression_should_return_cupons()
        {
            var coupons = _service.GetAllCoupons(c => true);

            coupons.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_cupons()
        {
            var coupons = _service.GetAllCoupons(c => c.Code == "asdag2356363hhfdhsdgs");

            coupons.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_params_should_return_cupons()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";
            var coupons = _service.GetAllCoupons(pageSize, pageNo, searchString);

            coupons.Count.ShouldBeGreaterThan(0);
            coupons.Coupons.Count.ShouldBeGreaterThan(0);
            coupons.PageSize.ShouldBe(pageSize);
            coupons.CurrentPage.ShouldBe(pageNo);
            coupons.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_invalid_search_string_should_return_empty_cupons()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "asfgasgw3t3434636erghedg";
            var coupons = _service.GetAllCoupons(pageSize, pageNo, searchString);

            coupons.Count.ShouldBe(0);
            coupons.Coupons.Count.ShouldBe(0);
            coupons.PageSize.ShouldBe(pageSize);
            coupons.CurrentPage.ShouldBe(pageNo);
            coupons.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_coupon_should_add()
        {
            var coupon = CreateCoupon(0);

            var id = _service.AddCoupon(coupon);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_coupon_should_throw_an_exception()
        {
            var coupon = CreateCoupon(0);
            coupon.Discount = -1;

            var exception = Should.Throw<BusinessException>(() => _service.AddCoupon(coupon));

            exception.Message.ShouldBe("Discount should be inclusive between 1 and 99");
        }

        [Fact]
        public void given_valid_coupon_should_update()
        {
            var coupon = CreateCoupon(0);
            var id = _service.AddCoupon(coupon);
            coupon = _service.Get(id);
            var code = "12345ABC";
            coupon.Code = code;

            _service.UpdateCoupon(coupon);

            var couponUpdated = _service.Get(id);
            couponUpdated.ShouldNotBeNull();
            couponUpdated.Code.ShouldBe(code);
        }

        [Fact]
        public void given_valid_id_should_delete_coupon()
        {
            var coupon = CreateCoupon(0);
            var id = _service.AddCoupon(coupon);

            _service.DeleteCoupon(id);

            var couponDeleted = _service.Get(id);
            couponDeleted.ShouldBeNull();
        }

        private CouponVm CreateCoupon(int id)
        {
            var coupon = new CouponVm
            {
                Id = id,
                Code = "SEGSWG21441",
                CouponTypeId = 1,
                Description = "Description",
                Discount = 15
            };
            return coupon;
        }
    }
}
