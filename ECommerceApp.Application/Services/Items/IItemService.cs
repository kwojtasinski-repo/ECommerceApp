using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Items
{
    public interface IItemService
    {
        ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        int Add(ItemVm vm);
        int AddItem(NewItemVm model);
        NewItemVm GetItemById(int id);
        void Update(ItemVm vm);
        void UpdateItem(NewItemVm model);
        List<NewItemVm> GetAllItems();
        IEnumerable<ItemVm> GetAllItems(Expression<Func<Item, bool>> expression);
        List<ItemsAddToCartVm> GetItemsAddToCart();
        void DeleteItem(int id);
        ItemDetailsVm GetItemDetails(int id);
        ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        bool ItemExists(int id);
    }
}
