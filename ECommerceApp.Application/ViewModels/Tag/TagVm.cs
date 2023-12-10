using ECommerceApp.Application.DTO;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Tag
{
    public class TagVm
    {
        public TagDto Tag { get; set; }
    }

    public class TagValidation : AbstractValidator<TagVm>
    {
        public TagValidation()
        {
            RuleFor(x => x.Tag).SetValidator(new TagDtoValidator());
        }
    }
}
