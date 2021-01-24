using AutoMapper;
using AutoMapper.QueryableExtensions;
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
    public class ItemService : ItemServiceAbstract
    {
        private readonly IItemRepository _itemRepo;
        private readonly IMapper _mapper;

        public ItemService(IItemRepository itemRepo, IMapper mapper) : base(itemRepo, mapper)
        {
            _itemRepo = itemRepo;
            _mapper = mapper;
        }

        public override int AddItem(NewItemVm model)
        {
            return Add(model);
        }

        public override int AddItemBrand(NewItemBrandVm model)
        {
            var brand = _mapper.Map<Brand>(model);
            var id = _itemRepo.AddItemBrand(brand);
            return id;
        }

        public override int AddItemType(NewItemTypeVm model)
        {
            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            var id = _itemRepo.AddItemType(type);
            return id;
        }

        public override ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString)
        {
            var items = GetAll(searchString);
            var itemsToShow = items.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemsList = new ListForItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Items = itemsToShow,
                Count = items.Count
            };

            return itemsList;
        }

        public override List<NewItemVm> GetAllItems()
        {
            return GetAll();
        }

        public override NewItemBrandVm GetItemBrandById(int id)
        {
            var brand = _itemRepo.GetItemBrandById(id);
            var brandVm = _mapper.Map<NewItemBrandVm>(brand);
            return brandVm;
        }

        public override NewItemTypeVm GetItemTypeById(int id)
        {
            var itemType = _itemRepo.GetItemTypeById(id);
            var itemTypeVm = _mapper.Map<NewItemTypeVm>(itemType);
            return itemTypeVm;
        }

        public override NewTagVm GetItemTagById(int id)
        {
            var itemTag = _itemRepo.GetItemTagById(id);
            var itemTagVm = _mapper.Map<NewTagVm>(itemTag);
            return itemTagVm;
        }

        public override NewItemVm GetItemById(int id)
        {
            return Get(id);
        }

        public override void UpdateItem(NewItemVm model)
        {
            Update(model);
        }

        public override void UpdateItemBrand(NewItemBrandVm model)
        {
            var brand = _mapper.Map<Brand>(model);
            _itemRepo.UpdateItemBrand(brand);
        }

        public override void UpdateItemType(NewItemTypeVm model)
        {
            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            _itemRepo.UpdateItemType(type);
        }

        public override void DeleteItem(int id)
        {
            Delete(id);
        }

        public override void DeleteItemType(int id)
        {
            _itemRepo.DeleteItemType(id);
        }

        public override void DeleteItemBrand(int id)
        {
            _itemRepo.DeleteItemBrand(id);
        }

        public override void DeleteItemTag(int id)
        {
            _itemRepo.DeleteTag(id);
        }

        public override ItemDetailsVm GetItemDetails(int id)
        {
            var item = _itemRepo.GetItemById(id);
            var itemVm = _mapper.Map<ItemDetailsVm>(item);

            return itemVm;
        }

        public override ListForItemTypeVm GetAllItemTypes(int pageSize, int pageNo, string searchString)
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

        public override List<TypeForListVm> GetAllItemTypes()
        {
            var types = _itemRepo.GetAllTypes()
                .ProjectTo<TypeForListVm>(_mapper.ConfigurationProvider)
                .ToList();

            return types;
        }

        public override List<BrandForListVm> GetAllItemBrands()
        {
            var brands = _itemRepo.GetAllBrands()
                .ProjectTo<BrandForListVm>(_mapper.ConfigurationProvider)
                .ToList();

            return brands;
        }

        public override ListForItemBrandVm GetAllItemBrands(int pageSize, int pageNo, string searchString)
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

        public override IQueryable<NewItemBrandVm> GetAllItemBrandsForAddingItems()
        {
            var itemBrands = _itemRepo.GetAllBrands();
            var itemBrandsVm = itemBrands.ProjectTo<NewItemBrandVm>(_mapper.ConfigurationProvider);
            return itemBrandsVm;
        }

        public override IQueryable<NewItemTypeVm> GetAllItemTypesForAddingItems()
        {
            var itemTypes = _itemRepo.GetAllTypes();
            var itemTypesVm = itemTypes.ProjectTo<NewItemTypeVm>(_mapper.ConfigurationProvider);
            return itemTypesVm;
        }

        public override int AddItemTag(NewTagVm model)
        {
            var itemTag = _mapper.Map<Tag>(model);
            var id = _itemRepo.AddItemTag(itemTag);
            return id;
        }

        public override ListForItemTagsVm GetAllTags(int pageSize, int pageNo, string searchString)
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

        public override List<TagForListVm> GetAllTags()
        {
            var tags = _itemRepo.GetAllTags()
                .ProjectTo<TagForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            
            return tags;
        }

        public override ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
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

        public override IQueryable<NewTagVm> GetAllItemTagsForAddingItems()
        {
            var itemTags = _itemRepo.GetAllTags();
            var itemTagsVm = itemTags.ProjectTo<NewTagVm>(_mapper.ConfigurationProvider);
            return itemTagsVm;
        }

        public override void UpdateItemTag(NewTagVm model)
        {
            var tag = _mapper.Map<Tag>(model);
            _itemRepo.UpdateTag(tag);
        }

        public override bool CheckIfItemExists(int id)
        {
            var item = _itemRepo.GetItemById(id);
            if (item == null)
            {
                return false;
            }
            return true;
        }

        public override bool CheckIfItemBrandExists(int id)
        {
            var brand = _itemRepo.GetItemBrandById(id);
            if (brand == null)
            {
                return false;
            }
            return true;
        }

        public override bool CheckIfItemTypeExists(int id)
        {
            var type = _itemRepo.GetItemTypeById(id);
            if (type == null)
            {
                return false;
            }
            return true;
        }

        public override bool CheckIfItemTagExists(int id)
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
