using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Tag;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemsWithTagsVm : IMapFrom<ECommerceApp.Domain.Model.ItemTag>
    {
        public int ItemId { get; set; }
        public int TagId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ItemTag, ItemsWithTagsVm>()
                .ForMember(i => i.ItemId, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(i => i.TagId, opt => opt.MapFrom(src => src.TagId))
                .ReverseMap();
        }
    }
}
