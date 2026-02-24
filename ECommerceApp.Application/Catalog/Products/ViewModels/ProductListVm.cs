using AutoMapper;
using ECommerceApp.Application.Mapping;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class ProductForListVm : IMapFrom<Domain.Catalog.Products.Item>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public int CategoryId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Catalog.Products.Item, ProductForListVm>()
                .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name.Value))
                .ForMember(d => d.Cost, opt => opt.MapFrom(s => s.Cost.Amount))
                .ForMember(d => d.CategoryId, opt => opt.MapFrom(s => s.CategoryId.Value))
                .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));
        }
    }

    public class ProductListVm
    {
        public List<ProductForListVm> Items { get; set; } = new();
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
