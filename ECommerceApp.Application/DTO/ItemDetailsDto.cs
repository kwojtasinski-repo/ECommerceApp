using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.DTO
{
    public class ItemDetailsDto : ItemDto
    {
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
        public IEnumerable<ImageDto> Images { get; set; }
    }
}
