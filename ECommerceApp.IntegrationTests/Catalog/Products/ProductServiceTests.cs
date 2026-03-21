using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Catalog.Products
{
    public class ProductServiceTests : BcBaseTest<IProductService>
    {
        private async Task<int> SeedCategoryAsync(string name = "Elektronika")
        {
            var repo = GetRequiredService<ICategoryRepository>();
            var category = Category.Create(name);
            var id = await repo.AddAsync(category);
            return id.Value;
        }

        // ── AddProduct ───────────────────────────────────────────────────

        [Fact]
        public async Task AddProduct_NullDto_ShouldThrowBusinessException()
        {
            await Should.ThrowAsync<BusinessException>(
                () => _service.AddProduct(null!));
        }

        [Fact]
        public async Task AddProduct_NonExistentCategory_ShouldThrowBusinessException()
        {
            var dto = new CreateProductDto(
                Name: "Test Product",
                Cost: 99.99m,
                Description: "Test",
                CategoryId: int.MaxValue,
                TagIds: Enumerable.Empty<int>());

            await Should.ThrowAsync<BusinessException>(
                () => _service.AddProduct(dto));
        }

        [Fact]
        public async Task AddProduct_ValidDto_ShouldReturnProductId()
        {
            var categoryId = await SeedCategoryAsync();

            var id = await _service.AddProduct(new CreateProductDto(
                Name: "Laptop",
                Cost: 3999.99m,
                Description: "Powerful laptop",
                CategoryId: categoryId,
                TagIds: Enumerable.Empty<int>()));

            id.ShouldBeGreaterThan(0);
        }

        // ── GetProductDetails ────────────────────────────────────────────

        [Fact]
        public async Task GetProductDetails_NonExistent_ShouldReturnNull()
        {
            var result = await _service.GetProductDetails(int.MaxValue);

            result.ShouldBeNull();
        }

        // ── GetAllProducts ───────────────────────────────────────────────

        [Fact]
        public async Task GetAllProducts_EmptyDatabase_ShouldReturnEmptyPage()
        {
            var result = await _service.GetAllProducts(pageSize: 10, pageNo: 1, searchString: "");

            result.ShouldNotBeNull();
            result.Products.ShouldNotBeNull();
            result.Products.ShouldBeEmpty();
        }

        // ── ProductExists ────────────────────────────────────────────────

        [Fact]
        public async Task ProductExists_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.ProductExists(int.MaxValue);

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ProductExists_Existing_ShouldReturnTrue()
        {
            var categoryId = await SeedCategoryAsync();
            var id = await _service.AddProduct(new CreateProductDto("Mouse", 49.99m, "Gaming", categoryId, Enumerable.Empty<int>()));

            var result = await _service.ProductExists(id);

            result.ShouldBeTrue();
        }

        // ── GetUnitPriceAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetUnitPriceAsync_NonExistent_ShouldReturnNull()
        {
            var result = await _service.GetUnitPriceAsync(int.MaxValue);

            result.ShouldBeNull();
        }

        // ── DeleteProduct ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteProduct_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.DeleteProduct(int.MaxValue);

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteProduct_Existing_ShouldReturnTrueAndRemoveProduct()
        {
            var categoryId = await SeedCategoryAsync();
            var id = await _service.AddProduct(new CreateProductDto("ToDelete", 10m, "Desc", categoryId, Enumerable.Empty<int>()));

            var result = await _service.DeleteProduct(id);

            result.ShouldBeTrue();

            var exists = await _service.ProductExists(id);
            exists.ShouldBeFalse();
        }

        // ── UpdateProduct ────────────────────────────────────────────────

        [Fact]
        public async Task UpdateProduct_NullDto_ShouldThrowBusinessException()
        {
            await Should.ThrowAsync<BusinessException>(
                () => _service.UpdateProduct(null!));
        }

        [Fact]
        public async Task UpdateProduct_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.UpdateProduct(new UpdateProductDto(
                Id: int.MaxValue, Name: "X", Cost: 1m, Description: "X",
                CategoryId: 1, TagIds: Enumerable.Empty<int>()));

            result.ShouldBeFalse();
        }

        // ── Full lifecycle ───────────────────────────────────────────────

        [Fact]
        public async Task FullLifecycle_AddUpdateDelete_ShouldWorkCorrectly()
        {
            var categoryId = await SeedCategoryAsync();

            // Add
            var id = await _service.AddProduct(new CreateProductDto(
                "Keyboard", 199.99m, "Mechanical keyboard", categoryId, Enumerable.Empty<int>()));
            id.ShouldBeGreaterThan(0);

            // Verify exists
            var exists = await _service.ProductExists(id);
            exists.ShouldBeTrue();

            // Update
            var updated = await _service.UpdateProduct(new UpdateProductDto(
                Id: id, Name: "Premium Keyboard", Cost: 249.99m,
                Description: "Premium mechanical keyboard",
                CategoryId: categoryId, TagIds: Enumerable.Empty<int>()));
            updated.ShouldBeTrue();

            // Delete
            var deleted = await _service.DeleteProduct(id);
            deleted.ShouldBeTrue();

            // Verify deleted
            var afterDelete = await _service.ProductExists(id);
            afterDelete.ShouldBeFalse();
        }
    }
}
