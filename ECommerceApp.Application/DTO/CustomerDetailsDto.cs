using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using static ECommerceApp.Application.DTO.ContactDetailDto;

namespace ECommerceApp.Application.DTO
{
    public class CustomerDetailsDto
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; }
        public string CompanyName { get; set; }

        public List<ContactDetailDto> ContactDetails { get; set; } = new();
        public virtual List<AddressDto> Addresses { get; set; } = new();
    }

    public class CustomerDetailsDtoValidator : AbstractValidator<CustomerDetailsDto>
    {
        public CustomerDetailsDtoValidator()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.FirstName).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.LastName).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.IsCompany).NotNull();
            When(x => x.IsCompany, () =>
            {
                RuleFor(x => x.NIP).NotNull().Length(9);
                RuleFor(x => x.CompanyName).NotNull().MinimumLength(3).MaximumLength(255);
            });
            When(x => x.ContactDetails is not null && x.ContactDetails.Any(), () =>
            {
                RuleForEach(x => x.ContactDetails).SetValidator(new ContactDetailDtoValidation());
            });
            When(x => x.Addresses is not null && x.Addresses.Any(), () =>
            {
                RuleForEach(x => x.Addresses).SetValidator(new AddressDtoValidation());
            });
        }
    }
}
