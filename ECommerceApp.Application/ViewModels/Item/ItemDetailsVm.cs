using ECommerceApp.Application.Catalog.Images.ViewModels;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemDetailsVm : BaseVm
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public string BrandName { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }

        public List<ItemTagForListVm> ItemTags { get; set; }
        public List<GetImageVm> Images { get; set; }
    }
}
