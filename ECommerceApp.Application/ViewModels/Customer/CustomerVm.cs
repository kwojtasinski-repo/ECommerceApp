using ECommerceApp.Application.DTO;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerVm
    {
        public CustomerDto Customer { get; set; }
        public List<ContactDetailDto> ContactDetails { get; set; } = new List<ContactDetailDto>();
        public List<ContactDetailTypeDto> ContactDetailTypes { get; set; } = new List<ContactDetailTypeDto>();
        public virtual List<AddressDto> Addresses { get; set; } = new List<AddressDto>();
    }

    public class CustomerVmValidation : AbstractValidator<CustomerVm>
    {
        public CustomerVmValidation()
        {
            RuleFor(x => x.Customer).NotNull().SetValidator(new CustomerDtoValidator());
        }
    }
}
