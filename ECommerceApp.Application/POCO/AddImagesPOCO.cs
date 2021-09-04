using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.POCO
{
    public class AddImagesPOCO
    {
        public int? ItemId { get; set; }
        public ICollection<IFormFile> Files { get; set; }
    }
}
