using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerRefundsVm : BaseVm, IMapFrom<Domain.Model.Refund>
    {
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; } // 1:1 Only one Order can be refund

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Model.Refund, CustomerRefundsVm>()
                .ForMember(r => r.Id, opt => opt.MapFrom(re => re.Id))
                .ForMember(r => r.Reason, opt => opt.MapFrom(re => re.Reason))
                .ForMember(r => r.Accepted, opt => opt.MapFrom(re => re.Accepted))
                .ForMember(r => r.OnWarranty, opt => opt.MapFrom(re => re.OnWarranty))
                .ForMember(r => r.CustomerId, opt => opt.MapFrom(re => re.CustomerId))
                .ForMember(r => r.RefundDate, opt => opt.MapFrom(re => re.RefundDate))
                .ForMember(r => r.OrderId, opt => opt.MapFrom(re => re.OrderId));
        }
    }
}