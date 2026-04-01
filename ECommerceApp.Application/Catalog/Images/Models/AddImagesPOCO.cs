using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Images.Models
{
    public class AddImagesPOCO
    {
        public int? ItemId { get; set; }
        public ICollection<IFormFile> Files { get; set; } = new List<IFormFile>();
    }
}
