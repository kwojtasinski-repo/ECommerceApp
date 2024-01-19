using System.Collections.Generic;

namespace ECommerceApp.Application.DTO
{
    public class UpdateItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public IEnumerable<int> TagsId { get; set; } = new List<int>();
        public IEnumerable<UpdateItemImageDto> Images { get; set; } = new List<UpdateItemImageDto>();
    }

    public record UpdateItemImageDto(int ImageId, string ImageName, string ImageSource);
}
