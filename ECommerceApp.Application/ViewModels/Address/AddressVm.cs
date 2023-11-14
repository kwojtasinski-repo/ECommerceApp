using ECommerceApp.Application.DTO;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Address
{
    public class AddressVm : BaseVm
    {
        public AddressDto Address { get; set; }
    }

    public class NewAddressValidation : AbstractValidator<AddressVm>
    {
        public NewAddressValidation()
        {
            RuleFor(a => a.Address).SetValidator(new AddressDtoValidation());
        }
    }
}
