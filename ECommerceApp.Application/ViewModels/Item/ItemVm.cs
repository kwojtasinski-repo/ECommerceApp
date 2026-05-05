using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemVm : BaseVm
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public List<ItemTagVm> ItemTags { get; set; }
    }
}
