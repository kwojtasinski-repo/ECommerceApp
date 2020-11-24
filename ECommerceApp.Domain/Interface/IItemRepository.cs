using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IItemRepository
    {
        void DeleteItem(int itemId);

        int AddItem(Item item);

        IQueryable<Item> GetItemsByTypeId(int typeId);

        Item GetItemById(int itemId);

        IQueryable<Tag> GetAllTags();

        IQueryable<ECommerceApp.Domain.Model.Type> GetAllTypes();
    }
}
