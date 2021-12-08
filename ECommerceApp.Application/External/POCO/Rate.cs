using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.External.POCO
{
    internal class Rate
    {
        public string No { get; set; }
        public DateTime EffectiveDate { get; set; }
        public decimal Mid { get; set; }
    }
}