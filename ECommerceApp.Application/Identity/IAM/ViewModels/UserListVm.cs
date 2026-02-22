using System.Collections.Generic;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class UserListVm
    {
        public IReadOnlyList<UserForListVm> Users { get; set; } = new List<UserForListVm>();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
