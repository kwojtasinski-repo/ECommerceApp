using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Items
{
    public class ItemService : IItemService
    {
        private readonly IMapper _mapper;
        private readonly IItemRepository _itemRepository;

        public ItemService(IItemRepository itemRepo, IMapper mapper)
        {
            _mapper = mapper;
            _itemRepository = itemRepo;
        }

        public int Add(ItemVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{vm.GetType().Name} cannot be null");
            }

            if (vm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = vm.MapToItem();
            var id = _itemRepository.Add(item);
            return id;
        }

        public ItemVm Get(int id)
        {
            var item = _itemRepository.GetById(id);
            if (item != null)
            {
                _itemRepository.DetachEntity(item);
            }
            var itemVm = item.MapToItemVm();
            return itemVm;
        }

        public void Update(ItemVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{vm.GetType().Name} cannot be null");
            }

            var item = vm.MapToItem();
            _itemRepository.Update(item);
        }


        public int AddItem(NewItemVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewItemVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = _mapper.Map<Item>(model);
            var id = _itemRepository.AddItem(item);
            return id;
        }

        public ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString)
        {
            var items = _itemRepository.GetAllItems()
                .Where(i => i.Name.StartsWith(searchString))
                .Skip(pageSize * (pageNo - 1)).Take(pageSize);
            var itemsToShow = items.ProjectTo<ItemDetailsVm>(_mapper.ConfigurationProvider).ToList();

            var itemsList = new ListForItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Items = itemsToShow,
                // TODO: Think about performance
                Count = _itemRepository.GetAll().Count()
            };

            return itemsList;
        }

        public List<NewItemVm> GetAllItems()
        {
            var items = _itemRepository.GetAllItems()
                .ProjectTo<NewItemVm>(_mapper.ConfigurationProvider)
                .ToList();
            return items;
        }

        public IEnumerable<ItemVm> GetAllItems(Expression<Func<Item, bool>> expression)
        {
            var items = _itemRepository.GetAll().Where(expression).ToList();
            var itemsVm = items.Select(i => i.MapToItemVm());
            return itemsVm;
        }

        public List<ItemsAddToCartVm> GetItemsAddToCart()
        {
            var items = _itemRepository.GetAll();
            var itemsVm = items.ProjectTo<ItemsAddToCartVm>(_mapper.ConfigurationProvider).ToList();
            return itemsVm;
        }

        public NewItemVm GetItemById(int id)
        {
            var item = _itemRepository.GetItemById(id);
            var itemVm = _mapper.Map<NewItemVm>(item);
            return itemVm;
        }

        public void UpdateItem(NewItemVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewItemVm).Name} cannot be null");
            }

            var item = _mapper.Map<Item>(model);
            _itemRepository.UpdateItem(item);
        }

        public void DeleteItem(int id)
        {
            _itemRepository.Delete(id);
        }

        public ItemDetailsVm GetItemDetails(int id)
        {
            var item = _itemRepository.GetItemById(id);
            var itemVm = _mapper.Map<ItemDetailsVm>(item);

            return itemVm;
        }

        public ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            var itemsWithTags = _itemRepository.GetAllItemsWithTags()//.Where(it => it.Item.Name.StartsWith(searchString))
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
            var exists = _itemRepository.ItemExists(id);
            return exists;
        }
    }
}
