using ECommerceApp.Application.ViewModels;

namespace ECommerceApp.Application.Catalog.Images.ViewModels
{
    public class GetImageVm : BaseVm
    {
        public string Name { get; set; }
        public int? ItemId { get; set; }
        public string ImageSource { get; set; }
    }
}
