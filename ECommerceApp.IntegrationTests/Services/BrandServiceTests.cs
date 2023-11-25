using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Brands;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Linq;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class BrandServiceTests : BaseTest<IBrandService>
    {
        [Fact]
        public void given_valid_id_should_return_brand()
        {
            var id = 1;
            var name = "Samsung";

            var brand = _service.GetBrand(id);

            brand.ShouldNotBeNull();
            brand.ShouldBeOfType<BrandDto>();
            brand.Name.ShouldBe(name);
        }

        [Fact]
        public void given_invalid_id_shouldnt_return_brand()
        {
            var id = 123;

            var brand = _service.GetBrand(id);

            brand.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_brand_details()
        {
            var id = 1;
            var name = "Samsung";

            var brand = _service.GetBrandDetail(id);

            brand.ShouldNotBeNull();
            brand.ShouldBeOfType<BrandDetailsVm>();
            brand.Name.ShouldBe(name);
        }

        [Fact]
        public void given_invalid_id_shouldnt_return_brand_details()
        {
            var id = 123;

            var brand = _service.GetBrandDetail(id);

            brand.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_brand_should_exists()
        {
            var id = 1;

            var exists = _service.BrandExists(id);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_invalid_id_brand_shouldnt_exists()
        {
            var id = 13252;

            var exists = _service.BrandExists(id);

            exists.ShouldBeFalse();
        }

        [Fact]
        public void given_valid_expression_should_return_brands()
        {
            var brands = _service.GetAllBrands(b => true);

            brands.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_brands()
        {
            var brands = _service.GetAllBrands(b => b.Name == "asfwewqeefw");

            brands.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_brand_should_add()
        {
            var brand = CreateBrand(0);

            var id = _service.AddBrand(brand);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_brand_should_throw_an_exception()
        {
            var brand = CreateBrand(234);

            var exception = Should.Throw<BusinessException>(() => _service.AddBrand(brand));

            exception.Message.ShouldBe("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_brand_should_update()
        {
            var brand = CreateBrand(0);
            var id = _service.AddBrand(brand);
            brand = _service.GetBrand(id);
            var name = "Brand1234";
            brand.Name = name;

            _service.UpdateBrand(brand);

            var brandUpdated = _service.GetBrand(id);
            brandUpdated.ShouldNotBeNull();
            brandUpdated.ShouldBeOfType<BrandDto>();
            brandUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public void given_valid_params_should_return_brands()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";
            
            var brands = _service.GetAllBrands(pageSize, pageNo, searchString);
            
            brands.Count.ShouldBeGreaterThan(0);
            brands.Brands.Count.ShouldBeGreaterThan(0);
            brands.CurrentPage.ShouldBe(pageNo);
            brands.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public void given_invalid_search_string_should_return_empty_brands()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "abcaswr14215";

            var brands = _service.GetAllBrands(pageSize, pageNo, searchString);

            brands.Count.ShouldBe(0);
            brands.Brands.Count.ShouldBe(0);
            brands.CurrentPage.ShouldBe(pageNo);
            brands.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public void given_valid_id_should_delete_brand()
        {
            var brand = CreateBrand(0);
            var id = _service.AddBrand(brand);

            _service.DeleteBrand(id);

            var brandDeleted = _service.GetBrand(id);
            brandDeleted.ShouldBeNull();
        }

        private BrandDto CreateBrand(int id)
        {
            var brand = new BrandDto
            {
                Id = id,
                Name = "Test"
            };
            return brand;
        }
    }
}
