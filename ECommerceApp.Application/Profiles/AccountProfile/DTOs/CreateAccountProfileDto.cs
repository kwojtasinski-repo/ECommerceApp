using FluentValidation;

namespace ECommerceApp.Application.Profiles.AccountProfile.DTOs
{
    public record CreateAccountProfileDto(
        string UserId,
        string FirstName,
        string LastName,
        bool IsCompany,
        string? NIP,
        string? CompanyName);

    public class CreateAccountProfileDtoValidator : AbstractValidator<CreateAccountProfileDto>
    {
        public CreateAccountProfileDtoValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.NIP).MaximumLength(50);
            RuleFor(x => x.CompanyName).MaximumLength(300);
            When(x => x.IsCompany, () =>
            {
                RuleFor(x => x.CompanyName).NotEmpty();
            });
        }
    }
}
