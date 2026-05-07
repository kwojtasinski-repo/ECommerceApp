using ECommerceApp.Application.DTO;
using FluentValidation;
using static ECommerceApp.Application.DTO.ContactDetailDto;

namespace ECommerceApp.Application.ViewModels.ContactDetail
{
    public class ContactDetailVm : BaseVm
    {
        public ContactDetailDto ContactDetail { get; set; }
    }

    public class ContactDetailVmValidation : AbstractValidator<ContactDetailVm>
    {
        public ContactDetailVmValidation()
        {
            RuleFor(a => a.ContactDetail).NotNull().SetValidator(new ContactDetailDtoValidation());
        }
    }
}
