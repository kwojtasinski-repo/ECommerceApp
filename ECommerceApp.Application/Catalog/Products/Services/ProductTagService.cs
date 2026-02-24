using AutoMapper;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class ProductTagService : IProductTagService
    {
        private readonly IProductTagRepository _repo;
        private readonly IMapper _mapper;

        public ProductTagService(IProductTagRepository repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<int> AddTag(CreateTagDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateTagDto)} cannot be null");

            var tag = Tag.Create(dto.Name, dto.Color, dto.IsVisible);
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

            tag.Update(dto.Name, dto.Color, dto.IsVisible);
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
            return tag is null ? null : _mapper.Map<ProductTagVm>(tag);
        }

        public async Task<List<ProductTagVm>> GetAllTags()
        {
            var tags = await _repo.GetAllAsync();
            return _mapper.Map<List<ProductTagVm>>(tags);
        }

        public async Task<List<ProductTagVm>> GetVisibleTags()
        {
            var tags = await _repo.GetAllVisibleAsync();
            return _mapper.Map<List<ProductTagVm>>(tags);
        }

        public async Task<List<ProductTagVm>> SearchTags(string query, int maxResults = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<ProductTagVm>();

            var tags = await _repo.SearchByNameAsync(query.Trim(), maxResults);
            return _mapper.Map<List<ProductTagVm>>(tags);
        }

        public async Task<ProductTagVm> GetOrCreateTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new BusinessException("Tag name cannot be empty");

            var tag = await _repo.GetOrCreateAsync(name.Trim());
            return _mapper.Map<ProductTagVm>(tag);
        }
    }
}
