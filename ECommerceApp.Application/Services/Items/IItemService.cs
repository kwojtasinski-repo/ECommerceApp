using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Items
{
    public interface IItemService
    {
        ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        int Add(ItemVm vm);
        int AddItem(NewItemVm model);
        int AddItem(AddItemDto dto);
        NewItemVm GetItemById(int id);
        void Update(ItemVm vm);
        void UpdateItem(NewItemVm model);
        void UpdateItem(UpdateItemDto dto);
        List<ItemDto> GetAllItems();
        List<ItemInfoVm> GetItemsAddToCart();
        void DeleteItem(int id);
        ItemDetailsDto GetItemDetails(int id);
        ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        bool ItemExists(int id);
    }
}
