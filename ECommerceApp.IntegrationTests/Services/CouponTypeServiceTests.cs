using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.CouponType;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class CouponTypeServiceTests : BaseTest<ICouponTypeService>
    {
        [Fact]
        public void given_valid_id_should_return_coupon_type()
        {
            var id = 1;
            var name = "Type1";

            var couponType = _service.GetCouponType(id);

            couponType.Id.ShouldBe(id);
            couponType.Type.ShouldBe(name);
        }

        [Fact]
        public void given_invalid_id_should_return_null()
        {
            var id = 125435;

            var couponType = _service.GetCouponType(id);

            couponType.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_coupon_type_detail()
        {
            var id = 1;
            var name = "Type1";

            var couponType = _service.GetCouponTypeDetail(id);

            couponType.Id.ShouldBe(id);
            couponType.Type.ShouldBe(name);
        }

        [Fact]
        public void given_invalid_id_should_return_null_coupon_type_detail()
        {
            var id = 125435;

            var couponType = _service.GetCouponTypeDetail(id);

            couponType.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_should_return_coupons()
        {
            var coupons = _service.GetAllCouponsTypes(ct => true);

            coupons.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_coupons()
        {
            var coupons = _service.GetAllCouponsTypes(ct => ct.Type == "ABca535tged");

            coupons.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_params_should_return_coupons()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var coupons = _service.GetAllCouponsTypes(pageSize, pageNo, searchString);

            coupons.Count.ShouldBeGreaterThan(0);
            coupons.CouponTypes.Count.ShouldBeGreaterThan(0);
            coupons.CurrentPage.ShouldBe(pageNo);
            coupons.PageSize.ShouldBe(pageSize);
            coupons.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_invalid_serach_string_should_return_empty_coupons()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "asdbsgtsgs52w5hd";

            var coupons = _service.GetAllCouponsTypes(pageSize, pageNo, searchString);

            coupons.Count.ShouldBe(0);
            coupons.CouponTypes.Count.ShouldBe(0);
            coupons.CurrentPage.ShouldBe(pageNo);
            coupons.PageSize.ShouldBe(pageSize);
            coupons.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_coupon_type_should_add()
        {
            var couponType = CreateCouponType(0);

            var id = _service.AddCouponType(couponType);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_coupon_type_should_add()
        {
            var couponType = CreateCouponType(1230);

            var exception = Should.Throw<BusinessException>(() => _service.AddCouponType(couponType));

            exception.Message.ShouldBe("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_coupon_type_should_update()
        {
            var couponType = CreateCouponType(0);
            var id = _service.AddCouponType(couponType);
            couponType = _service.Get(id);
            var type = "TypeCouponTest12345";
            couponType.Type = type;

            _service.UpdateCouponType(couponType);

            var couponUpdated = _service.Get(id);
            couponUpdated.ShouldNotBeNull();
            couponUpdated.Type.ShouldBe(type);
        }

        [Fact]
        public void given_valid_id_should_delete_coupon_type()
        {
            var couponType = CreateCouponType(0);
            var id = _service.AddCouponType(couponType);

            _service.DeleteCouponType(id);

            var couponTypeDeleted = _service.Get(id);
            couponTypeDeleted.ShouldBeNull();
        }

        private CouponTypeVm CreateCouponType(int id)
        {
            var couponType = new CouponTypeVm
            {
                Id = id,
                Type = "Type235256"
            };
            return couponType;
        }
    }
}
