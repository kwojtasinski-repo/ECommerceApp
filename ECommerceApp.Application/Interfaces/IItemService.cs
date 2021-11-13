using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IItemService : IAbstractService<ItemVm, IItemRepository, Item>
    {
        ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        int AddItem(NewItemVm model);
        int AddItemType(NewItemTypeVm model);
        NewItemVm GetItemById(int id);
        void UpdateItem(NewItemVm model);
        NewItemTypeVm GetItemTypeById(int id);
        void UpdateItemType(NewItemTypeVm model);
        List<NewItemVm> GetAllItems();
        void DeleteItem(int id);
        void DeleteItemType(int id);
        void DeleteItemTag(int id);
        ItemDetailsVm GetItemDetails(int id);
        ListForItemTypeVm GetAllItemTypes(int pageSize, int pageNo, string searchString);
        IQueryable<NewItemTypeVm> GetAllItemTypesForAddingItems();
        List<TypeForListVm> GetAllItemTypes();
        int AddItemTag(NewTagVm model);
        ListForItemTagsVm GetAllTags(int pageSize, int pageNo, string searchString);
        IQueryable<NewTagVm> GetAllItemTagsForAddingItems();
        ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        List<TagForListVm> GetAllTags();
        NewTagVm GetItemTagById(int id);
        void UpdateItemTag(NewTagVm model);
        bool CheckIfItemExists(int id);
        bool CheckIfItemTypeExists(int id);
        bool CheckIfItemTagExists(int id);
    }
}
