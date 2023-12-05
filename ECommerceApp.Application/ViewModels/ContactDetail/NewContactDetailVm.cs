using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using FluentValidation;
using System.Collections.Generic;
using static ECommerceApp.Application.DTO.ContactDetailDto;

namespace ECommerceApp.Application.ViewModels.ContactDetail
{
    public class NewContactDetailVm
    {
        public ContactDetailDto ContactDetail { get; set; }
        public List<ContactDetailTypeVm> ContactDetailTypes { get; set; }
    }

    public class NewContactDetailValidation : AbstractValidator<NewContactDetailVm>
    {
        public NewContactDetailValidation()
        {
            RuleFor(x => x.ContactDetail).SetValidator(new ContactDetailDtoValidation());
        }
    }
}
