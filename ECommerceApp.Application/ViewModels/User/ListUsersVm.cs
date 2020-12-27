using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.User
{
    public class ListUsersVm
    {
        public List<UserForListVm> Users { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
