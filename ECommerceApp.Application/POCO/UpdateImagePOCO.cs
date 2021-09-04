using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace ECommerceApp.Application.POCO
{
    public class UpdateImagePOCO
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ItemId { get; set; }
    }
}
