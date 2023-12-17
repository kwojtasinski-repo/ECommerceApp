using ECommerceApp.Application.DTO;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Type
{
    public class TypeVm
    {
        public TypeDto Type { get; set; }
    }

    public class TypeVmValidation : AbstractValidator<TypeVm>
    {
        public TypeVmValidation()
        {
            RuleFor(x => x.Type).SetValidator(new TypeDtoValidator());
        }
    }
}
