using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Items
{
    public interface IItemService
    {
        ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString);
        ListForItemVm GetAllAvailableItemsForList(int pageSize, int pageNo, string searchString);
        int AddItem(AddItemDto dto);
        bool UpdateItem(UpdateItemDto dto);
        List<ItemDto> GetAllItems();
        List<ItemInfoVm> GetItemsAddToCart();
        bool DeleteItem(int id);
        ItemDetailsDto GetItemDetails(int id);
        ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString);
        bool ItemExists(int id);
    }
}
