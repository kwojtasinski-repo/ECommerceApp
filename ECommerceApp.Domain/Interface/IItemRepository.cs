using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IItemRepository
    {
        bool DeleteItem(int itemId);
        int AddItem(Item item);
        Item GetItemById(int itemId);
        Item GetItemDetailsById(int itemId);
        List<Item> GetAllItems();
        List<Item> GetAllItems(int pageSize, int pageNo, string searchString);
        List<Item> GetItemInfos();
        void UpdateItem(Item item);
        List<ItemTag> GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        bool ExistsById(int id);
        int GetCountItemTagsBySearchString(string searchString);
        int GetCountBySearchString(string searchString);
        List<Item> GetItemsByIds(IEnumerable<int> enumerable);
    }
}
