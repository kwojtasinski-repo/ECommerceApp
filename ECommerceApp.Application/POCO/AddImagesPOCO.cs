using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace ECommerceApp.Application.POCO
{
    public class AddImagesPOCO
    {
        public int? ItemId { get; set; }
        public ICollection<IFormFile> Files { get; set; }
    }
}
