using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class ProductTagVm : IMapFrom<Domain.Catalog.Products.Tag>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Catalog.Products.Tag, ProductTagVm>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.Value))
                .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name.Value))
                .ForMember(d => d.Slug, opt => opt.MapFrom(s => s.Slug.Value));
        }
    }
}
