﻿using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ListForItemVm
    {
        public List<ItemDetailsVm> Items { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
