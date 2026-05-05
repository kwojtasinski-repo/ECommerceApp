using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class ContactDetailTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ContactDetailTypeDtoValidator : AbstractValidator<ContactDetailTypeDto>
    {
        public ContactDetailTypeDtoValidator()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
