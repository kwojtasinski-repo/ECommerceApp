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
        public string ItemName { get; set; }
        public NewItemVm Item { get; set; }
        public int TagId { get; set; }
        public string TagName { get; set; }
        public TagDetailsVm Tag { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ItemTag, ItemsWithTagsVm>()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(m => m.Item.Name))
                .ForMember(i => i.TagName, opt => opt.MapFrom(m => m.Tag.Name));
        }
    }
}
