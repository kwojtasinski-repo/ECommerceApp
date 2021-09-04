using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ECommerceApp.Application.ViewModels.Image
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
