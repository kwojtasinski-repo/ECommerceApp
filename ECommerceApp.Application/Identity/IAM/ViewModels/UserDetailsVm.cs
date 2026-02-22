using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class UserDetailsVm : IMapFrom<ApplicationUser>
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string UserRole { get; set; } = "";
        public IReadOnlyList<RoleVm> AvailableRoles { get; set; } = new List<RoleVm>();
        public string NewPassword { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ApplicationUser, UserDetailsVm>();
        }
    }

    public class UserDetailsVmValidator : AbstractValidator<UserDetailsVm>
    {
        public UserDetailsVmValidator()
        {
            RuleFor(x => x.UserName).NotNull().WithMessage("Nazwa użytkownika nie może być pusta")
                                    .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Email).NotNull().WithMessage("Email nie może być pusty")
                                 .EmailAddress().WithMessage("Proszę podać email");
        }
    }
}
