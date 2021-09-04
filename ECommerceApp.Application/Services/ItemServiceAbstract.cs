using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public abstract class ItemServiceAbstract : IBaseService<NewItemVm>
    {
        private readonly IItemRepository _itemRepo;
        private readonly IMapper _mapper;

        public ItemServiceAbstract(IItemRepository itemRepo, IMapper mapper)
        {
            _itemRepo = itemRepo;
            _mapper = mapper;
        }

        public int Add(NewItemVm objectVm)
        {
            if (objectVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = _mapper.Map<Item>(objectVm);
            var id = _itemRepo.AddItem(item);
            return id;
        }

        public void Delete(int id)
        {
            _itemRepo.DeleteItem(id);
        }

        public NewItemVm Get(int id)
        {
            var item = _itemRepo.GetItemById(id);
            var itemVm = _mapper.Map<NewItemVm>(item);
            return itemVm;
        }

        public List<NewItemVm> GetAll()
        {
            var items = _itemRepo.GetAllItems()
                .ProjectTo<NewItemVm>(_mapper.ConfigurationProvider)
                .ToList();
            return items;
        }

        public List<NewItemVm> GetAll(string searchName)
        {
            var items = _itemRepo.GetAllItems().Where(it => it.Name.StartsWith(searchName))
                .ProjectTo<NewItemVm>(_mapper.ConfigurationProvider)
                .ToList();
            return items;
        }

        public void Update(NewItemVm objectVm)
        {
            var item = _mapper.Map<Item>(objectVm);
            _itemRepo.UpdateItem(item);
        }

        public abstract int AddItem(NewItemVm model);
        public abstract int AddItemBrand(NewItemBrandVm model);
        public abstract int AddItemType(NewItemTypeVm model);
        public abstract ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        public abstract List<NewItemVm> GetAllItems();
        public abstract NewItemBrandVm GetItemBrandById(int id);
        public abstract NewItemTypeVm GetItemTypeById(int id);
        public abstract NewTagVm GetItemTagById(int id);
        public abstract NewItemVm GetItemById(int id);
        public abstract void UpdateItem(NewItemVm model);
        public abstract void UpdateItemBrand(NewItemBrandVm model);
        public abstract void UpdateItemType(NewItemTypeVm model);
        public abstract void DeleteItem(int id);
        public abstract void DeleteItemType(int id);
        public abstract void DeleteItemBrand(int id);
        public abstract void DeleteItemTag(int id);
        public abstract ItemDetailsVm GetItemDetails(int id);
        public abstract ListForItemTypeVm GetAllItemTypes(int pageSize, int pageNo, string searchString);
        public abstract List<TypeForListVm> GetAllItemTypes();
        public abstract List<BrandForListVm> GetAllItemBrands();
        public abstract ListForItemBrandVm GetAllItemBrands(int pageSize, int pageNo, string searchString);
        public abstract IQueryable<NewItemBrandVm> GetAllItemBrandsForAddingItems();
        public abstract IQueryable<NewItemTypeVm> GetAllItemTypesForAddingItems();
        public abstract int AddItemTag(NewTagVm model);
        public abstract ListForItemTagsVm GetAllTags(int pageSize, int pageNo, string searchString);
        public abstract List<TagForListVm> GetAllTags();
        public abstract ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        public abstract IQueryable<NewTagVm> GetAllItemTagsForAddingItems();
        public abstract void UpdateItemTag(NewTagVm model);
        public abstract bool CheckIfItemExists(int id);
        public abstract bool CheckIfItemBrandExists(int id);
        public abstract bool CheckIfItemTypeExists(int id);
        public abstract bool CheckIfItemTagExists(int id);
    }
}
