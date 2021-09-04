using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.POCO
{
    public class AddImagePOCO
    {
        public int? ItemId { get; set; }
        public IFormFile File { get; set; }
    }
}
