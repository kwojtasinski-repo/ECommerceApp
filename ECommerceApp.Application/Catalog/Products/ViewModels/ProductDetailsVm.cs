using AutoMapper;
using ECommerceApp.Application.Mapping;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class ProductDetailsVm : IMapFrom<Domain.Catalog.Products.Item>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int CategoryId { get; set; }
        public List<ProductImageVm> Images { get; set; } = new();
        public List<int> TagIds { get; set; } = new();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Catalog.Products.Item, ProductDetailsVm>()
                .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name.Value))
                .ForMember(d => d.Cost, opt => opt.MapFrom(s => s.Cost.Amount))
                .ForMember(d => d.CategoryId, opt => opt.MapFrom(s => s.CategoryId.Value))
                .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.Images, opt => opt.Ignore())
                .ForMember(d => d.TagIds, opt => opt.MapFrom(s => s.ItemTags.Select(t => t.TagId.Value).ToList()));
        }
    }

    public class ProductImageVm
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
    }
}
