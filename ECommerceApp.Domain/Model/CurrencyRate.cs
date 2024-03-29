﻿using System;

namespace ECommerceApp.Domain.Model
{
    public class CurrencyRate : BaseEntity
    {
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }
        public decimal Rate { get; set; }
        public DateTime CurrencyDate { get; set; }
    }
}
