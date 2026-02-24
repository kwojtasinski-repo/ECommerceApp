using AutoMapper;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class ProductService : IProductService
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IProductTagRepository _tagRepo;
        private readonly IImageUrlBuilder _urlBuilder;
        private readonly IMapper _mapper;

        public ProductService(
            IProductRepository productRepo,
            ICategoryRepository categoryRepo,
            IProductTagRepository tagRepo,
            IImageUrlBuilder urlBuilder,
            IMapper mapper)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _tagRepo = tagRepo;
            _urlBuilder = urlBuilder;
            _mapper = mapper;
        }

        public async Task<int> AddProduct(CreateProductDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateProductDto)} cannot be null");

            if (!await _categoryRepo.ExistsByIdAsync(new CategoryId(dto.CategoryId)))
                throw new BusinessException(
                    $"Category with id '{dto.CategoryId}' was not found",
                    ErrorCode.Create("categoryNotFound", ErrorParameter.Create("id", dto.CategoryId)));

            var (product, _) = Product.Create(dto.Name, dto.Cost, dto.Quantity, dto.Description, dto.CategoryId);

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

        public async Task<ProductDetailsVm> GetProductDetails(int id)
        {
            var product = await _productRepo.GetByIdWithDetailsAsync(new ProductId(id));
            if (product is null)
                return null;

            var vm = _mapper.Map<ProductDetailsVm>(product);
            vm.Images = product.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageVm
                {
                    Id = i.Id.Value,
                    Url = _urlBuilder.Build(i.FileName.Value),
                    IsMain = i.IsMain,
                    SortOrder = i.SortOrder
                })
                .ToList();

            var category = await _categoryRepo.GetByIdAsync(new CategoryId(product.CategoryId.Value));
            vm.CategoryName = category?.Name.Value ?? string.Empty;

            return vm;
        }

        public async Task<ProductListVm> GetAllProducts(int pageSize, int pageNo, string searchString)
        {
            var products = await _productRepo.GetAllAsync(pageSize, pageNo, searchString);
            var count = await _productRepo.CountAsync(searchString);
            return new ProductListVm
            {
                Items = _mapper.Map<List<ProductForListVm>>(products),
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
            return new ProductListVm
            {
                Items = _mapper.Map<List<ProductForListVm>>(products),
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
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
        }

        public async Task UnpublishProduct(int id)
        {
            var product = await _productRepo.GetByIdAsync(new ProductId(id));
            if (product is null)
                throw new BusinessException(
                    $"Product with id '{id}' was not found",
                    ErrorCode.Create("productNotFound", ErrorParameter.Create("id", id)));

            product.Unpublish();
            await _productRepo.UpdateAsync(product);
        }

        public async Task<bool> ProductExists(int id)
        {
            return await _productRepo.ExistsByIdAsync(new ProductId(id));
        }
    }
}
