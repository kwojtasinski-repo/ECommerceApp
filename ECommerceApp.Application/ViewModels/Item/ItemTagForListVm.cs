using ECommerceApp.Application.DTO;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemTagForListVm
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public ItemDetailsVm Item { get; set; }
        public int TagId { get; set; }
        public string TagName { get; set; }
        public TagDto Tag { get; set; }
    }
}
