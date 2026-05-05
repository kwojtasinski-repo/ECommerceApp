using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Catalog.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class ProductService : IProductService
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IProductTagRepository _tagRepo;
        private readonly IImageUrlBuilder _urlBuilder;
        private readonly IMessageBroker _broker;

        public ProductService(
            IProductRepository productRepo,
            ICategoryRepository categoryRepo,
            IProductTagRepository tagRepo,
            IImageUrlBuilder urlBuilder,
            IMessageBroker broker)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _tagRepo = tagRepo;
            _urlBuilder = urlBuilder;
            _broker = broker;
        }

        public async Task<int> AddProduct(CreateProductDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateProductDto)} cannot be null");

            if (!await _categoryRepo.ExistsByIdAsync(new CategoryId(dto.CategoryId)))
                throw new BusinessException(
                    $"Category with id '{dto.CategoryId}' was not found",
                    ErrorCode.Create("categoryNotFound", ErrorParameter.Create("id", dto.CategoryId)));

            var product = Product.Create(dto.Name, dto.Cost, dto.Description, dto.CategoryId);

            if (dto.TagIds is not null)
            {
                foreach (var tagId in dto.TagIds)
                    product.AddTag(new TagId(tagId));
            }

            var id = await _productRepo.AddAsync(product);
            return id.Value;
        }

        public async Task<bool> UpdateProduct(UpdateProductDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(UpdateProductDto)} cannot be null");

            var product = await _productRepo.GetByIdWithDetailsAsync(new ProductId(dto.Id));
            if (product is null)
                return false;

            if (!await _categoryRepo.ExistsByIdAsync(new CategoryId(dto.CategoryId)))
                throw new BusinessException(
                    $"Category with id '{dto.CategoryId}' was not found",
                    ErrorCode.Create("categoryNotFound", ErrorParameter.Create("id", dto.CategoryId)));

            product.UpdateDetails(dto.Name, dto.Cost, dto.Description, dto.CategoryId);

            if (dto.TagIds is not null)
                product.ReplaceTags(dto.TagIds.Select(id => new TagId(id)));

            await _productRepo.UpdateAsync(product);
            return true;
        }

        public async Task<bool> DeleteProduct(int id)
        {
            return await _productRepo.DeleteAsync(new ProductId(id));
        }

        public async Task<ProductDetailsVm> GetProductDetails(int id, CancellationToken cancellationToken = default)
        {
            var product = await _productRepo.GetByIdWithDetailsAsync(new ProductId(id), cancellationToken);
            if (product is null)
            {
                return null;
            }

            var vm = ProductDetailsVm.FromDomain(product);
            vm.Images = product.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageVm
                {
                    Id = i.Id.Value,
                    FileName = i.FileName.Value,
                    Url = _urlBuilder.Build(i.Id.Value),
                    IsMain = i.IsMain,
                    SortOrder = i.SortOrder
                })
                .ToList();

            var category = await _categoryRepo.GetByIdAsync(new CategoryId(product.CategoryId.Value));
            vm.CategoryName = category?.Name.Value ?? string.Empty;

            var tagIds = product.ProductTags.Select(pt => pt.TagId.Value).ToList();
            if (tagIds.Count > 0)
            {
                var tags = await _tagRepo.GetByIdsAsync(tagIds);
                vm.TagNames = tags.Select(t => t.Name.Value).ToList();
            }

            return vm;
        }

        public async Task<ProductListVm> GetAllProducts(int pageSize, int pageNo, string searchString)
        {
            var products = await _productRepo.GetAllAsync(pageSize, pageNo, searchString);
            var count = await _productRepo.CountAsync(searchString);
            return new ProductListVm
            {
                Products = products.Select(ProductForListVm.FromDomain).ToList(),
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Count = count
            };
        }

        public async Task<ProductListVm> GetPublishedProducts(int pageSize, int pageNo, string searchString)
        {
            var products = await _productRepo.GetPublishedAsync(pageSize, pageNo, searchString);
            var count = await _productRepo.CountPublishedAsync(searchString);
            var mapped = products.Select(ProductForListVm.FromDomain).ToList();
            for (var i = 0; i < mapped.Count; i++)
            {
                var mainImage = products[i].Images.FirstOrDefault(img => img.IsMain);
                if (mainImage is not null)
                {
                    mapped[i].MainImageUrl = _urlBuilder.Build(mainImage.Id.Value);
                }
            }
            return new ProductListVm
            {
                Products = mapped,
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Count = count
            };
        }

        public async Task<ProductListVm> GetPublishedProductsByTagAsync(int tagId, int pageSize, int pageNo)
        {
            var products = await _productRepo.GetPublishedByTagAsync(tagId, pageSize, pageNo);
            var count = await _productRepo.CountPublishedByTagAsync(tagId);
            var mapped = products.Select(ProductForListVm.FromDomain).ToList();
            for (var i = 0; i < mapped.Count; i++)
            {
                var main = products[i].Images.FirstOrDefault(img => img.IsMain);
                if (main is not null)
                    mapped[i].MainImageUrl = _urlBuilder.Build(main.Id.Value);
            }
            return new ProductListVm
            {
                Products = mapped,
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = string.Empty,
                Count = count
            };
        }

        public async Task PublishProduct(int id)
        {
            var product = await _productRepo.GetByIdAsync(new ProductId(id));
            if (product is null)
                throw new BusinessException(
                    $"Product with id '{id}' was not found",
                    ErrorCode.Create("productNotFound", ErrorParameter.Create("id", id)));

            product.Publish();
            await _productRepo.UpdateAsync(product);
            await _broker.PublishAsync(new ProductPublished(product.Id.Value, product.Name.Value, false, DateTime.UtcNow));
        }

        public async Task UnpublishProduct(int id)
        {
            var product = await _productRepo.GetByIdAsync(new ProductId(id));
            if (product is null)
                throw new BusinessException(
                    $"Product with id '{id}' was not found",
                    ErrorCode.Create("productNotFound", ErrorParameter.Create("id", id)));

            var @event = product.Unpublish(Domain.Catalog.Products.UnpublishReason.ManualReview);
            await _productRepo.UpdateAsync(product);
            await _broker.PublishAsync(new ProductUnpublished(product.Id.Value, @event.Reason, DateTime.UtcNow));
        }

        public async Task<bool> ProductExists(int id)
        {
            return await _productRepo.ExistsByIdAsync(new ProductId(id));
        }

        public async Task<decimal?> GetUnitPriceAsync(int id, CancellationToken ct = default)
        {
            return await _productRepo.GetUnitPriceAsync(new ProductId(id), ct);
        }

        public async Task<IReadOnlyList<ProductNameImageDto>> GetProductSnapshotsByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
        {
            var products = await _productRepo.GetByIdsWithImagesAsync(ids, ct);
            var snapshots = new List<ProductNameImageDto>(products.Count);
            foreach (var product in products)
            {
                var mainImage = product.Images.FirstOrDefault(i => i.IsMain)
                    ?? product.Images.OrderBy(i => i.SortOrder).FirstOrDefault();
                var imageUrl = mainImage is not null ? _urlBuilder.Build(mainImage.Id.Value) : null;
                snapshots.Add(new ProductNameImageDto(product.Id.Value, product.Name.Value, mainImage?.FileName?.Value, imageUrl, mainImage?.Id.Value));
            }
            return snapshots;
        }
    }
}
