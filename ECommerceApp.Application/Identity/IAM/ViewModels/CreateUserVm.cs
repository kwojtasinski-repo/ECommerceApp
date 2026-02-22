using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class CreateUserVm
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string Password { get; set; }
        public string UserRole { get; set; }
        public IReadOnlyList<RoleVm> AvailableRoles { get; set; } = new List<RoleVm>();
    }

    public class CreateUserVmValidator : AbstractValidator<CreateUserVm>
    {
        public CreateUserVmValidator()
        {
            RuleFor(x => x.UserName).NotNull().WithMessage("Nazwa użytkownika nie może być pusta")
                                    .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Email).NotNull().WithMessage("Email nie może być pusty")
                                 .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Password).NotNull().WithMessage("Hasło nie może być puste")
                                    .Matches("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{8,}$")
                                    .WithMessage("Hasło musi zawierać przynajmniej jedną dużą i małą literę oraz jeden specjalny znak" +
                                    ", a także nie może być krótsze niż 8 znaków");
            RuleFor(x => x.UserRole).NotNull().NotEmpty();
        }
    }
}
