using AutoMapper;
using ECommerceApp.Application.Mapping;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Type
{
    public class TypeDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Type>
    {
        public string Name { get; set; }

        public List<ECommerceApp.Domain.Model.Item> Items { get; set; } = new List<Domain.Model.Item>();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<TypeDetailsVm, ECommerceApp.Domain.Model.Type>().ReverseMap()
                .ForMember(p => p.Items, opt => opt.MapFrom(ps => ps.Items));
        }
    }
}
