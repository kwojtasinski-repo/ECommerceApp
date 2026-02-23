using FluentValidation;

namespace ECommerceApp.Application.Profiles.AccountProfile.DTOs
{
    public record UpdateAccountProfileDto(
        int Id,
        string FirstName,
        string LastName,
        bool IsCompany,
        string? NIP,
        string? CompanyName);

    public class UpdateAccountProfileDtoValidator : AbstractValidator<UpdateAccountProfileDto>
    {
        public UpdateAccountProfileDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
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
