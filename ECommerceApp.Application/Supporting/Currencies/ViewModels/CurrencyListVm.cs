using System.Collections.Generic;

namespace ECommerceApp.Application.Supporting.Currencies.ViewModels
{
    public class CurrencyListVm
    {
        public List<CurrencyVm> Currencies { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
