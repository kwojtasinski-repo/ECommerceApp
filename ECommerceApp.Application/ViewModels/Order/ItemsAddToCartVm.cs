using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class ItemsAddToCartVm : IMapFrom<ECommerceApp.Domain.Model.Item>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        public List<ItemsAddToCartVm> Items { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ItemsAddToCartVm, ECommerceApp.Domain.Model.Item>().ReverseMap();
        }
    }
}
