using FluentValidation;

namespace ECommerceApp.Application.AccountProfile.DTOs
{
    public record CreateUserProfileDto(
        string UserId,
        string FirstName,
        string LastName,
        bool IsCompany,
        string? NIP,
        string? CompanyName,
        string Email,
        string PhoneNumber);

    public class CreateUserProfileDtoValidator : AbstractValidator<CreateUserProfileDto>
    {
        public CreateUserProfileDtoValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.NIP).MaximumLength(50);
            RuleFor(x => x.CompanyName).MaximumLength(300);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(300);
            RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
            When(x => x.IsCompany, () => RuleFor(x => x.CompanyName).NotEmpty());
        }
    }
}
