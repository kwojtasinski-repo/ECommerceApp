using AutoMapper;
using ECommerceApp.Application.Mapping;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Item>
    {
        public string Name { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public string BrandName { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }

        public List<ItemTagForListVm> ItemTags { get; set; }
        public List<Image.GetImageVm> Images { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Item, ItemDetailsVm>()
                .ForMember(i => i.BrandName, opt => opt.MapFrom(d => d.Brand.Name))
                .ForMember(i => i.TypeName, opt => opt.MapFrom(d => d.Type.Name))
                .ForMember(i => i.CurrencyName, opt => opt.MapFrom(d => d.Currency.Code))
                .ForMember(i => i.Images, opt => opt.Ignore());
        }
    }
}
