using ECommerceApp.Application.DTO;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Type
{
    public class ListForTypeVm
    {
        public List<TypeDto> Types { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
