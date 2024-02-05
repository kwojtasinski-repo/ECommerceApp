using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;

namespace ECommerceApp.Application.ViewModels.Refund
{
    public class RefundVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Refund>
    {
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; } // 1:1 Only one Order can be refund

        public void Mapping(Profile profile)
        {
            profile.CreateMap<RefundVm, ECommerceApp.Domain.Model.Refund>().ReverseMap();
        }
    }

    public class NewRefundValidation : AbstractValidator<RefundVm>
    {
        public NewRefundValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Reason).MaximumLength(255);
            RuleFor(x => x.Accepted).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OnWarranty).NotNull();
            RuleFor(x => x.RefundDate).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}