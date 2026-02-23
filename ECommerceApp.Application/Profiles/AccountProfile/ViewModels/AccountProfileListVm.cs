using System.Collections.Generic;

namespace ECommerceApp.Application.Profiles.AccountProfile.ViewModels
{
    public class AccountProfileListVm
    {
        public List<AccountProfileForListVm> Profiles { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
