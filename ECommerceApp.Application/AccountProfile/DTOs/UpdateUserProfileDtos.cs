using FluentValidation;

namespace ECommerceApp.Application.AccountProfile.DTOs
{
    public record UpdateUserProfileDto(
        int Id,
        string FirstName,
        string LastName,
        bool IsCompany,
        string? NIP,
        string? CompanyName);

    public record UpdateContactInfoDto(int Id, string Email, string PhoneNumber);

    public class UpdateUserProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
    {
        public UpdateUserProfileDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.NIP).MaximumLength(50);
            RuleFor(x => x.CompanyName).MaximumLength(300);
            When(x => x.IsCompany, () => RuleFor(x => x.CompanyName).NotEmpty());
        }
    }

    public class UpdateContactInfoDtoValidator : AbstractValidator<UpdateContactInfoDto>
    {
        public UpdateContactInfoDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(300);
            RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        }
    }
}
