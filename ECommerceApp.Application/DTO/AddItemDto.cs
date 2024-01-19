using System.Collections.Generic;

namespace ECommerceApp.Application.DTO
{
    public class AddItemDto
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public IEnumerable<int> TagsId { get; set; } = new List<int>();
        public IEnumerable<AddItemImageDto> Images { get; set; } = new List<AddItemImageDto>();
    }

    public record AddItemImageDto(string ImageName, string ImageSource);
}