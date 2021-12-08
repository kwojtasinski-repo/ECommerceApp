using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.External.POCO
{
    internal class ExchangeRate
    {
        public string Table { get; set; }
        public string Currency { get; set; }
        public string Code { get; set; }
        public List<Rate> Rates { get; set; }
    }
}
