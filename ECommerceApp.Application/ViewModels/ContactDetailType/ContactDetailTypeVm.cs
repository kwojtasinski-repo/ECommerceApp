using ECommerceApp.Application.DTO;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.ContactDetailType
{
    public class ContactDetailTypeVm
    {
        public ContactDetailTypeDto ContactDetailType { get; set; }
    }

    public class ContactDetailTypeValidation : AbstractValidator<ContactDetailTypeVm>
    {
        public ContactDetailTypeValidation()
        {
            RuleFor(x => x.ContactDetailType).SetValidator(new ContactDetailTypeDtoValidator());
        }
    }
}
