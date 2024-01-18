using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.DTO
{
    public class ItemDto : IMapFrom<Item>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public BrandDto Brand { get; set; }
        public TypeDto Type { get; set; }
        public CurrencyDto Currency { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Item, ItemDto>()
                .ReverseMap();
        }
    }
}
