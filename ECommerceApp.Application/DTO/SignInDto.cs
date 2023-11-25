using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public record SignInDto(string Email, string Password);

    public class SignInDtoValidator : AbstractValidator<SignInDto>
    {
        public SignInDtoValidator()
        {
            RuleFor(s => s.Email).NotEmpty().EmailAddress().MaximumLength(255);
            RuleFor(s => s.Password).NotEmpty().MaximumLength(64);
        }
    }
}
