using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IItemRepository : IGenericRepository<Item>
    {
        void DeleteItem(int itemId);

        int AddItem(Item item);

        IQueryable<Item> GetItemsByTypeId(int typeId);

        Item GetItemById(int itemId);

        IQueryable<Tag> GetAllTags();

        IQueryable<ECommerceApp.Domain.Model.Type> GetAllTypes();
        IQueryable<Item> GetAllItems();
        int AddItemBrand(Brand brand);
        int AddItemType(Model.Type type);
        void UpdateItem(Item item);
        void UpdateItemType(Model.Type type);
        void UpdateItemBrand(Brand brand);
        Model.Type GetItemTypeById(int id);
        Brand GetItemBrandById(int id);
        void DeleteItemType(int id);
        void DeleteItemBrand(int id);
        IQueryable<Brand> GetAllBrands();
        int AddItemTag(Tag tag);
        IQueryable<ItemTag> GetAllItemsWithTags();
        Tag GetItemTagById(int id);
        void UpdateTag(Tag tag);
        void DeleteTag(int id);
        ItemTag GetItemTagByItemId(int itemId);
        void AddItemTag(ItemTag itemIag);
        IQueryable<ItemTag> GetAllItemTags();
    }
}
