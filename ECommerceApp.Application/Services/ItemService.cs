using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Type;
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
        public ItemService(IItemRepository itemRepo, IMapper mapper) : base(itemRepo, mapper)
        { }

        public override int Add(ItemVm vm)
        {
            if (vm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = vm.MapToItem();
            var id = _repo.Add(item);
            return id;
        }

        public override ItemVm Get(int id)
        {
            var item = _repo.GetById(id);
            var itemVm = new ItemVm().MapToItemVm(item);
            return itemVm;
        }

        public override void Delete(ItemVm vm)
        {
            var item = vm.MapToItem();
            _repo.Delete(item);
        }

        public override void Update(ItemVm vm)
        {
            var item = vm.MapToItem();
            _repo.Update(item);
        }


        public int AddItem(NewItemVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = _mapper.Map<Item>(model);
            var id = _repo.AddItem(item);
            return id;
        }

        public ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString)
        {
            var items = _repo.GetAllItems().Skip(pageSize * (pageNo - 1)).Take(pageSize);
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
            var items = _repo.GetAllItems()
                .ProjectTo<NewItemVm>(_mapper.ConfigurationProvider)
                .ToList();
            return items;
        }

        public TypeVm GetItemTypeById(int id)
        {
            var itemType = _repo.GetItemTypeById(id);
            var itemTypeVm = _mapper.Map<TypeVm>(itemType);
            return itemTypeVm;
        }

        public NewTagVm GetItemTagById(int id)
        {
            var itemTag = _repo.GetItemTagById(id);
            var itemTagVm = _mapper.Map<NewTagVm>(itemTag);
            return itemTagVm;
        }

        public NewItemVm GetItemById(int id)
        {
            var item = _repo.GetItemById(id);
            var itemVm = _mapper.Map<NewItemVm>(item);
            return itemVm;
        }

        public void UpdateItem(NewItemVm model)
        {
            var item = _mapper.Map<Item>(model);
            _repo.UpdateItem(item);
        }

        public void UpdateItemType(TypeVm model)
        {
            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            _repo.UpdateItemType(type);
        }

        public void DeleteItem(int id)
        {
            Delete(id);
        }

        public void DeleteItemType(int id)
        {
            _repo.DeleteItemType(id);
        }

        public void DeleteItemTag(int id)
        {
            _repo.DeleteTag(id);
        }

        public ItemDetailsVm GetItemDetails(int id)
        {
            var item = _repo.GetItemById(id);
            var itemVm = _mapper.Map<ItemDetailsVm>(item);

            return itemVm;
        }

        public ListForTypeVm GetAllItemTypes(int pageSize, int pageNo, string searchString)
        {
            var types = _repo.GetAllTypes().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TypeVm>(_mapper.ConfigurationProvider)
                .ToList();
            var typesToShow = types.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var typesList = new ListForTypeVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Types = typesToShow,
                Count = types.Count
            };

            return typesList;
        }

        public List<TypeVm> GetAllItemTypes()
        {
            var types = _repo.GetAllTypes()
                .ProjectTo<TypeVm>(_mapper.ConfigurationProvider)
                .ToList();

            return types;
        }

        public IQueryable<TypeVm> GetAllItemTypesForAddingItems()
        {
            var itemTypes = _repo.GetAllTypes();
            var itemTypesVm = itemTypes.ProjectTo<TypeVm>(_mapper.ConfigurationProvider);
            return itemTypesVm;
        }

        public int AddItemTag(NewTagVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var itemTag = _mapper.Map<Tag>(model);
            var id = _repo.AddItemTag(itemTag);
            return id;
        }

        public ListForItemTagsVm GetAllTags(int pageSize, int pageNo, string searchString)
        {
            var tags = _repo.GetAllTags().Where(it => it.Name.StartsWith(searchString))
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
            var tags = _repo.GetAllTags()
                .ProjectTo<TagForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            
            return tags;
        }

        public ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            var itemsWithTags = _repo.GetAllItemsWithTags()//.Where(it => it.Item.Name.StartsWith(searchString))
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
            var itemTags = _repo.GetAllTags();
            var itemTagsVm = itemTags.ProjectTo<NewTagVm>(_mapper.ConfigurationProvider);
            return itemTagsVm;
        }

        public void UpdateItemTag(NewTagVm model)
        {
            var tag = _mapper.Map<Tag>(model);
            _repo.UpdateTag(tag);
        }

        public bool CheckIfItemExists(int id)
        {
            var item = _repo.GetItemById(id);
            if (item == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfItemTypeExists(int id)
        {
            var type = _repo.GetItemTypeById(id);
            if (type == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfItemTagExists(int id)
        {
            var tag = _repo.GetItemTagById(id);
            if (tag == null)
            {
                return false;
            }
            return true;
        }
    }
}
