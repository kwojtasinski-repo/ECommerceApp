using ECommerceApp.Domain.Identity.IAM;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class UserDetailsVm
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string UserRole { get; set; } = "";
        public IReadOnlyList<RoleVm> AvailableRoles { get; set; } = new List<RoleVm>();
        public string NewPassword { get; set; }

        public static UserDetailsVm FromDomain(ApplicationUser s) => new()
        {
            Id = s.Id,
            UserName = s.UserName,
            Email = s.Email,
            EmailConfirmed = s.EmailConfirmed
        };
    }

    public class UserDetailsVmValidator : AbstractValidator<UserDetailsVm>
    {
        public UserDetailsVmValidator()
        {
            RuleFor(x => x.UserName).NotNull().WithMessage("Nazwa użytkownika nie może być pusta")
                                    .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Email).NotNull().WithMessage("Email nie może być pusty")
                                 .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.NewPassword)
                .Matches("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{8,}$")
                .WithMessage("Hasło musi zawierać przynajmniej jedną dużą i małą literę oraz jeden specjalny znak" +
                    ", a także nie może być krótsze niż 8 znaków")
                .When(x => !string.IsNullOrWhiteSpace(x.NewPassword));
        }
    }
}
