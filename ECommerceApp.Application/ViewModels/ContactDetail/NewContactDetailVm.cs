using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.ContactDetail
{
    public class NewContactDetailVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public string ContactDetailTypeName { get; set; }
        public int CustomerId { get; set; }
        public List<ContactDetailTypeVm> ContactDetailTypes { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewContactDetailVm, ECommerceApp.Domain.Model.ContactDetail>().ReverseMap()
                .ForMember(s => s.ContactDetailTypeName, opt => opt.MapFrom(d => d.ContactDetailType.Name));
        }
    }

    public class NewContactDetailValidation : AbstractValidator<NewContactDetailVm>
    {
        public NewContactDetailValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.ContactDetailTypeId).NotNull();
            RuleFor(x => x.ContactDetailTypeName).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
