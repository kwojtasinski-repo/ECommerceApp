using ECommerceApp.Application.ViewModels;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Images.ViewModels
{
    public class ImageVm : BaseVm
    {
        public string SourcePath { get; set; }
        public string Name { get; set; }
        public int? ItemId { get; set; }

        public byte[] ImageSource { get; set; }

        [JsonIgnore]
        public ICollection<IFormFile> Images { get; set; }
    }
}
