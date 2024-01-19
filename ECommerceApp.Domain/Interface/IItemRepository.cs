using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IItemRepository : IGenericRepository<Item>
    {
        void DeleteItem(int itemId);
        int AddItem(Item item);
        IQueryable<Item> GetItemsByTypeId(int typeId);
        Item GetItemById(int itemId);
        Item GetItemDetailsById(int itemId);
        IQueryable<Item> GetAllItems();
        void UpdateItem(Item item);
        IQueryable<ItemTag> GetAllItemsWithTags();
        bool ItemExists(int id);
    }
}
