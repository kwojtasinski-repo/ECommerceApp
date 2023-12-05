using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using static ECommerceApp.Application.DTO.ContactDetailDto;

namespace ECommerceApp.Application.ViewModels.ContactDetail
{
    public class ContactDetailVm : BaseVm, IMapFrom<Domain.Model.ContactDetail>
    {
        public ContactDetailDto ContactDetail { get; set; }
    }

    public class ContactDetailVmValidation : AbstractValidator<ContactDetailVm>
    {
        public ContactDetailVmValidation()
        {
            RuleFor(a => a.ContactDetail).SetValidator(new ContactDetailDtoValidation());
        }
    }
}
