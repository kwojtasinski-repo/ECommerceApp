﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Currency
{
    public class ListCurrencyVm
    {
        public List<CurrencyVm> Currencies { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
