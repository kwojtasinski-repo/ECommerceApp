using AutoMapper;
using ECommerceApp.Application.Mapping;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class ItemInfoVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Item>
    {
        public string Name { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ItemInfoVm, ECommerceApp.Domain.Model.Item>().ReverseMap();
        }
    }
}
