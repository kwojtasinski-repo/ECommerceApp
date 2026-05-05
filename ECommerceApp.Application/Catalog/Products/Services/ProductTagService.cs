using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class ProductTagService : IProductTagService
    {
        private readonly IProductTagRepository _repo;

        public ProductTagService(IProductTagRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> AddTag(CreateTagDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateTagDto)} cannot be null");

            var tag = Tag.Create(dto.Name);
            var id = await _repo.AddAsync(tag);
            return id.Value;
        }

        public async Task<bool> UpdateTag(UpdateTagDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(UpdateTagDto)} cannot be null");

            var tag = await _repo.GetByIdAsync(new TagId(dto.Id));
            if (tag is null)
                return false;

            tag.Update(dto.Name);
            await _repo.UpdateAsync(tag);
            return true;
        }

        public async Task<bool> DeleteTag(int id)
        {
            return await _repo.DeleteAsync(new TagId(id));
        }

        public async Task<ProductTagVm> GetTag(int id)
        {
            var tag = await _repo.GetByIdAsync(new TagId(id));
            return tag is null ? null : ProductTagVm.FromDomain(tag);
        }

        public async Task<List<ProductTagVm>> GetAllTags()
        {
            var tags = await _repo.GetAllAsync();
            return tags.Select(ProductTagVm.FromDomain).ToList();
        }

        public async Task<List<ProductTagVm>> SearchTags(string query, int maxResults = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<ProductTagVm>();

            var tags = await _repo.SearchByNameAsync(query.Trim(), maxResults);
            return tags.Select(ProductTagVm.FromDomain).ToList();
        }

        public async Task<ProductTagVm> GetOrCreateTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new BusinessException("Tag name cannot be empty");

            var tag = await _repo.GetOrCreateAsync(name.Trim());
            return ProductTagVm.FromDomain(tag);
        }

        public async Task<List<TagWithUsageVm>> GetTagsWithUsageAsync(int maxProductsPerTag = 10)
        {
            var summaries = await _repo.GetUsageSummariesAsync(maxProductsPerTag);
            return summaries.Select(s => new TagWithUsageVm
            {
                Id = s.Id.Value,
                Name = s.Name,
                Slug = s.Slug,
                TotalProductCount = s.TotalProductCount,
                TopProductNames = s.TopProductNames.ToList()
            }).ToList();
        }
    }
}
