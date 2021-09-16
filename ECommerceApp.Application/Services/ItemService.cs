using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
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
    public class ItemService : AbstractService<ItemVm, IItemRepository, Item>, IItemService
    {
        private readonly IItemRepository _itemRepo;
        private readonly IMapper _mapper;

        public ItemService(IItemRepository itemRepo, IMapper mapper) : base(itemRepo, mapper)
        {
            _itemRepo = itemRepo;
            _mapper = mapper;
        }

        public override int Add(ItemVm vm)
        {
            if (vm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = vm.MapToItem();
            var id = _itemRepo.Add(item);
            return id;
        }

        public override ItemVm Get(int id)
        {
            var item = _itemRepo.GetById(id);
            var itemVm = new ItemVm().MapToItemVm(item);
            return itemVm;
        }

        public override void Delete(ItemVm vm)
        {
            var item = vm.MapToItem();
            _itemRepo.Delete(item);
        }

        public override void Update(ItemVm vm)
        {
            var item = vm.MapToItem();
            _itemRepo.Update(item);
        }


        public int AddItem(NewItemVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = _mapper.Map<Item>(model);
            var id = _itemRepo.AddItem(item);
            return id;
        }

        public int AddItemBrand(NewItemBrandVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var brand = _mapper.Map<Brand>(model);
            var id = _itemRepo.AddItemBrand(brand);
            return id;
        }

        public int AddItemType(NewItemTypeVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            var id = _itemRepo.AddItemType(type);
            return id;
        }

        public ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString)
        {
            var items = _itemRepo.GetAllItems().Skip(pageSize * (pageNo - 1)).Take(pageSize);
            var itemsToShow = items.ProjectTo<ItemDetailsVm>(_mapper.ConfigurationProvider).ToList();

            var itemsList = new ListForItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Items = itemsToShow,
                Count = itemsToShow.Count
            };

            return itemsList;
        }

        public List<NewItemVm> GetAllItems()
        {
            var items = _itemRepo.GetAllItems()
                .ProjectTo<NewItemVm>(_mapper.ConfigurationProvider)
                .ToList();
            return items;
        }

        public NewItemBrandVm GetItemBrandById(int id)
        {
            var brand = _itemRepo.GetItemBrandById(id);
            var brandVm = _mapper.Map<NewItemBrandVm>(brand);
            return brandVm;
        }

        public NewItemTypeVm GetItemTypeById(int id)
        {
            var itemType = _itemRepo.GetItemTypeById(id);
            var itemTypeVm = _mapper.Map<NewItemTypeVm>(itemType);
            return itemTypeVm;
        }

        public NewTagVm GetItemTagById(int id)
        {
            var itemTag = _itemRepo.GetItemTagById(id);
            var itemTagVm = _mapper.Map<NewTagVm>(itemTag);
            return itemTagVm;
        }

        public NewItemVm GetItemById(int id)
        {
            var item = _itemRepo.GetItemById(id);
            var itemVm = _mapper.Map<NewItemVm>(item);
            return itemVm;
        }

        public void UpdateItem(NewItemVm model)
        {
            var item = _mapper.Map<Item>(model);
            _itemRepo.UpdateItem(item);
        }

        public void UpdateItemBrand(NewItemBrandVm model)
        {
            var brand = _mapper.Map<Brand>(model);
            _itemRepo.UpdateItemBrand(brand);
        }

        public void UpdateItemType(NewItemTypeVm model)
        {
            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            _itemRepo.UpdateItemType(type);
        }

        public void DeleteItem(int id)
        {
            Delete(id);
        }

        public void DeleteItemType(int id)
        {
            _itemRepo.DeleteItemType(id);
        }

        public void DeleteItemBrand(int id)
        {
            _itemRepo.DeleteItemBrand(id);
        }

        public void DeleteItemTag(int id)
        {
            _itemRepo.DeleteTag(id);
        }

        public ItemDetailsVm GetItemDetails(int id)
        {
            var item = _itemRepo.GetItemById(id);
            var itemVm = _mapper.Map<ItemDetailsVm>(item);

            return itemVm;
        }

        public ListForItemTypeVm GetAllItemTypes(int pageSize, int pageNo, string searchString)
        {
            var types = _itemRepo.GetAllTypes().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TypeForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            var typesToShow = types.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var typesList = new ListForItemTypeVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Types = typesToShow,
                Count = types.Count
            };

            return typesList;
        }

        public List<TypeForListVm> GetAllItemTypes()
        {
            var types = _itemRepo.GetAllTypes()
                .ProjectTo<TypeForListVm>(_mapper.ConfigurationProvider)
                .ToList();

            return types;
        }

        public List<BrandForListVm> GetAllItemBrands()
        {
            var brands = _itemRepo.GetAllBrands()
                .ProjectTo<BrandForListVm>(_mapper.ConfigurationProvider)
                .ToList();

            return brands;
        }

        public ListForItemBrandVm GetAllItemBrands(int pageSize, int pageNo, string searchString)
        {
            var brands = _itemRepo.GetAllBrands().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<BrandForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            var brandsToShow = brands.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var brandsList = new ListForItemBrandVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Brands = brandsToShow,
                Count = brands.Count
            };

            return brandsList;
        }

        public IQueryable<NewItemBrandVm> GetAllItemBrandsForAddingItems()
        {
            var itemBrands = _itemRepo.GetAllBrands();
            var itemBrandsVm = itemBrands.ProjectTo<NewItemBrandVm>(_mapper.ConfigurationProvider);
            return itemBrandsVm;
        }

        public IQueryable<NewItemTypeVm> GetAllItemTypesForAddingItems()
        {
            var itemTypes = _itemRepo.GetAllTypes();
            var itemTypesVm = itemTypes.ProjectTo<NewItemTypeVm>(_mapper.ConfigurationProvider);
            return itemTypesVm;
        }

        public int AddItemTag(NewTagVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var itemTag = _mapper.Map<Tag>(model);
            var id = _itemRepo.AddItemTag(itemTag);
            return id;
        }

        public ListForItemTagsVm GetAllTags(int pageSize, int pageNo, string searchString)
        {
            var tags = _itemRepo.GetAllTags().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TagForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            var tagsToShow = tags.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var tagsList = new ListForItemTagsVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Tags = tagsToShow,
                Count = tags.Count
            };

            return tagsList;
        }

        public List<TagForListVm> GetAllTags()
        {
            var tags = _itemRepo.GetAllTags()
                .ProjectTo<TagForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            
            return tags;
        }

        public ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            var itemsWithTags = _itemRepo.GetAllItemsWithTags()//.Where(it => it.Item.Name.StartsWith(searchString))
                .ProjectTo<ItemsWithTagsVm>(_mapper.ConfigurationProvider)
                .ToList();
            var itemsWithTagsToShow = itemsWithTags.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemsWithTagsList = new ListForItemWithTagsVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemTags = itemsWithTagsToShow,
                Count = itemsWithTags.Count
            };

            return itemsWithTagsList;
        }

        public IQueryable<NewTagVm> GetAllItemTagsForAddingItems()
        {
            var itemTags = _itemRepo.GetAllTags();
            var itemTagsVm = itemTags.ProjectTo<NewTagVm>(_mapper.ConfigurationProvider);
            return itemTagsVm;
        }

        public void UpdateItemTag(NewTagVm model)
        {
            var tag = _mapper.Map<Tag>(model);
            _itemRepo.UpdateTag(tag);
        }

        public bool CheckIfItemExists(int id)
        {
            var item = _itemRepo.GetItemById(id);
            if (item == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfItemBrandExists(int id)
        {
            var brand = _itemRepo.GetItemBrandById(id);
            if (brand == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfItemTypeExists(int id)
        {
            var type = _itemRepo.GetItemTypeById(id);
            if (type == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfItemTagExists(int id)
        {
            var tag = _itemRepo.GetItemTagById(id);
            if (tag == null)
            {
                return false;
            }
            return true;
        }
    }
}
