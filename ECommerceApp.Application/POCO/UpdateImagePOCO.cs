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
