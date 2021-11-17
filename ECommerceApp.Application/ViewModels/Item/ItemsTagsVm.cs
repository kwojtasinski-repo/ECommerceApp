using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Tag;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemsTagsVm : IMapFrom<ECommerceApp.Domain.Model.ItemTag>
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int TagId { get; set; }
        public string TagName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ItemTag, ItemsTagsVm>()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(m => m.Item.Name))
                .ForMember(i => i.TagName, opt => opt.MapFrom(m => m.Tag.Name));
        }
    }
}
