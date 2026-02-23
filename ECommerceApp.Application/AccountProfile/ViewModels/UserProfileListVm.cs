using System.Collections.Generic;

namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class UserProfileListVm
    {
        public List<UserProfileForListVm> Profiles { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
