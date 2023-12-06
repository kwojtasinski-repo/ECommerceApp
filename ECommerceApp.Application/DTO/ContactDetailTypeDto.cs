using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class ContactDetailTypeDto : IMapFrom<Domain.Model.ContactDetail>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Model.ContactDetailType, ContactDetailTypeDto>().ReverseMap();
        }
    }

    public class ContactDetailTypeDtoValidator : AbstractValidator<ContactDetailTypeDto>
    {
        public ContactDetailTypeDtoValidator()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
