using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class NewTagVm : IMapFrom<ECommerceApp.Domain.Model.Tag>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        List<ItemsWithTagsVm> ItemTags { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewTagVm, ECommerceApp.Domain.Model.Tag>().ReverseMap();
        }
    }
}
