using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.ContactDetailType
{
    public class ContactDetailTypeVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ContactDetailType, ContactDetailTypeVm>().ReverseMap();
        }
    }

    public class ContactDetailTypeValidation : AbstractValidator<ContactDetailTypeVm>
    {
        public ContactDetailTypeValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
