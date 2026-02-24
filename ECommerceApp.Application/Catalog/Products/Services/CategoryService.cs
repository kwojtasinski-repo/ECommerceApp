using AutoMapper;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repo;
        private readonly IMapper _mapper;

        public CategoryService(ICategoryRepository repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<int> AddCategory(CreateCategoryDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateCategoryDto)} cannot be null");

            var category = Category.Create(dto.Name);
            var id = await _repo.AddAsync(category);
            return id.Value;
        }

        public async Task<bool> UpdateCategory(UpdateCategoryDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(UpdateCategoryDto)} cannot be null");

            var category = await _repo.GetByIdAsync(new CategoryId(dto.Id));
            if (category is null)
                return false;

            category.Update(dto.Name);
            await _repo.UpdateAsync(category);
            return true;
        }

        public async Task<bool> DeleteCategory(int id)
        {
            return await _repo.DeleteAsync(new CategoryId(id));
        }

        public async Task<CategoryVm> GetCategory(int id)
        {
            var category = await _repo.GetByIdAsync(new CategoryId(id));
            return category is null ? null : _mapper.Map<CategoryVm>(category);
        }

        public async Task<List<CategoryVm>> GetAllCategories()
        {
            var categories = await _repo.GetAllAsync();
            return _mapper.Map<List<CategoryVm>>(categories);
        }
    }
}
