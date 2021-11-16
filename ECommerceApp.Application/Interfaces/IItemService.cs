using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IItemService : IAbstractService<ItemVm, IItemRepository, Item>
    {
        ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        int AddItem(NewItemVm model);
        NewItemVm GetItemById(int id);
        void UpdateItem(NewItemVm model);
        List<NewItemVm> GetAllItems();
        IEnumerable<ItemVm> GetAllItems(Expression<Func<Item, bool>> expression);
        List<ItemsAddToCartVm> GetItemsAddToCart();
        void DeleteItem(int id);
        ItemDetailsVm GetItemDetails(int id);
        ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        bool ItemExists(int id);
        IQueryable<NewItemVm> GetItems();
    }
}
