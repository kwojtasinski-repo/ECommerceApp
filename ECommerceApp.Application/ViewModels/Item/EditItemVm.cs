using ECommerceApp.Application.DTO;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class EditItemVm
    {
        public ItemDetailsDto Item { get; private set; }

        public EditItemVm(ItemDetailsDto itemDetailsDto)
        {
            Item = itemDetailsDto;
            ItemTags = itemDetailsDto.Tags.Select(t => t.Id).ToList() ?? new List<int>();
        }

        public List<BrandDto> Brands { get; set; } = new List<BrandDto>();
        public List<TypeDto> Types { get; set; } = new List<TypeDto>();
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
        public List<int> ItemTags { get; set; } = new List<int>();
    }
}
