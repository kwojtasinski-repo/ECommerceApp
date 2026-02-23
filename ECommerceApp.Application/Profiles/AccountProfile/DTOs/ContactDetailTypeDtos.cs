using FluentValidation;

namespace ECommerceApp.Application.Profiles.AccountProfile.DTOs
{
    public record AddContactDetailTypeDto(string Name);

    public record UpdateContactDetailTypeDto(int Id, string Name);

    public class AddContactDetailTypeDtoValidator : AbstractValidator<AddContactDetailTypeDto>
    {
        public AddContactDetailTypeDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        }
    }

    public class UpdateContactDetailTypeDtoValidator : AbstractValidator<UpdateContactDetailTypeDto>
    {
        public UpdateContactDetailTypeDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        }
    }
}
