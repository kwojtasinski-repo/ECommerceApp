using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.External.POCO
{
    internal class ExchangeRateTable
    {
        public string Table { get; set; }
        public string No { get; set; }
        public DateTime EffectiveDate { get; set; }
        public List<TableRate> Rates { get; set; }
    }

    internal class TableRate
    {
        public string Currency { get; set; }
        public string Code { get; set; }
        public decimal Mid { get; set; }
    }
}
