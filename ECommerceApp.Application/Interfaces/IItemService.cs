using ECommerceApp.Application.ViewModels.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IItemService
    {
        ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        int AddItem(NewItemVm model);
        int AddItemBrand(NewItemBrandVm model);
        int AddItemType(NewItemTypeVm model);
        NewItemVm GetItemById(int id);
        void UpdateItem(NewItemVm model);
        NewItemBrandVm GetItemBrandById(int id);
        NewItemTypeVm GetItemTypeById(int id);
        void UpdateItemBrand(NewItemBrandVm model);
        void UpdateItemType(NewItemTypeVm model);
        List<ItemForListVm> GetAllItems();
        void DeleteItem(int id);
        void DeleteItemType(int id);
        void DeleteItemBrand(int id);
        void DeleteItemTag(int id);
        ItemDetailsVm GetItemDetails(int id);
        ListForItemTypeVm GetAllItemTypes(int pageSize, int pageNo, string searchString);
        List<BrandForListVm> GetAllItemBrands();
        ListForItemBrandVm GetAllItemBrands(int pageSize, int pageNo, string searchString);
        IQueryable<NewItemTypeVm> GetAllItemTypesForAddingItems();
        IQueryable<NewItemBrandVm> GetAllItemBrandsForAddingItems();
        List<TypeForListVm> GetAllItemTypes();
        int AddItemTag(NewTagVm model);
        ListForItemTagsVm GetAllTags(int pageSize, int pageNo, string searchString);
        IQueryable<NewTagVm> GetAllItemTagsForAddingItems();
        ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        List<TagForListVm> GetAllTags();
        NewTagVm GetItemTagById(int id);
        void UpdateItemTag(NewTagVm model);
        bool CheckIfItemExists(int id);
        bool CheckIfItemBrandExists(int id);
        bool CheckIfItemTypeExists(int id);
        bool CheckIfItemTagExists(int id);
    }
}
