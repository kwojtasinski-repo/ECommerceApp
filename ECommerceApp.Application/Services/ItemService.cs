using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services
{
    public class ItemService : AbstractService<ItemVm, IItemRepository, Item>, IItemService
    {
        public ItemService(IItemRepository itemRepo, IMapper mapper) : base(itemRepo, mapper)
        {}

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
            var itemVm = item.MapToItemVm();
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
            var items = _repo.GetAllItems()
                .Where(i => i.Name.StartsWith(searchString))
                .Skip(pageSize * (pageNo - 1)).Take(pageSize);
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

        public IEnumerable<ItemVm> GetAllItems(Expression<Func<Item, bool>> expression)
        {
            var items = _repo.GetAll().Where(expression).ToList();
            var itemsVm = items.Select(i => i.MapToItemVm());
            return itemsVm;
        }

        public List<ItemsAddToCartVm> GetItemsAddToCart()
        {
            var items = _repo.GetAll();
            var itemsVm = items.ProjectTo<ItemsAddToCartVm>(_mapper.ConfigurationProvider).ToList();
            return itemsVm;
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

        public void DeleteItem(int id)
        {
            Delete(id);
        }

        public ItemDetailsVm GetItemDetails(int id)
        {
            var item = _repo.GetItemById(id);
            var itemVm = _mapper.Map<ItemDetailsVm>(item);

            return itemVm;
        }

        public ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            var itemsWithTags = _repo.GetAllItemsWithTags()//.Where(it => it.Item.Name.StartsWith(searchString))
                .ProjectTo<ItemsTagsVm>(_mapper.ConfigurationProvider)
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

        public bool ItemExists(int id)
        {
            var exists = _repo.ItemExists(id);
            return exists;
        }

        public IQueryable<NewItemVm> GetItems()
        {
            return _repo.GetAll().ProjectTo<NewItemVm>(_mapper.ConfigurationProvider);
        }
    }
}
