using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class ContactDetailDto : IMapFrom<Domain.Model.ContactDetail>
    {
        public int Id { get; set; }
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public int CustomerId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ContactDetailDto, Domain.Model.ContactDetail>()
                .ForMember(c => c.Id, opt => opt.MapFrom(co => co.Id))
                .ForMember(c => c.ContactDetailInformation, opt => opt.MapFrom(co => co.ContactDetailInformation))
                .ForMember(c => c.ContactDetailTypeId, opt => opt.MapFrom(co => co.ContactDetailTypeId))
                .ForMember(c => c.CustomerId, opt => opt.MapFrom(co => co.CustomerId))
                .ReverseMap();
        }

        public class ContactDetailDtoValidation : AbstractValidator<ContactDetailDto>
        {
            public ContactDetailDtoValidation()
            {
                RuleFor(x => x.Id).NotNull();
                When(x => x.ContactDetailTypeId == 1, () =>
                {
                    RuleFor(x => x.ContactDetailInformation).Length(9);
                });
                When(x => x.ContactDetailTypeId == 2, () =>
                {
                    RuleFor(x => x.ContactDetailInformation).EmailAddress();
                });
                RuleFor(x => x.ContactDetailInformation).NotNull().MaximumLength(100);
                RuleFor(x => x.ContactDetailTypeId).NotNull();
                RuleFor(x => x.CustomerId).NotNull();
            }
        }
    }
}
