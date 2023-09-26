using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ECommerceApp.Application.ViewModels.User
{
    public class NewUserToAddVm : IMapFrom<ApplicationUser>
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string Password { get; set; }
        public List<string> UserRoles { get; set; }
        public List<RoleVm> Roles { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewUserVm, ApplicationUser>();
        }
    }

    public class NewUserToAddValidation : AbstractValidator<NewUserToAddVm>
    {
        public NewUserToAddValidation()
        {
            RuleFor(x => x.UserName).NotNull().WithMessage("Nazwa użytkownika nie może być pusta")
                                    .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Email).NotNull().WithMessage("Email nie może być pusty")
                                 .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Password).NotNull().WithMessage("Hasło nie może być puste")
                                  .Matches("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{8,}$")
                                  .WithMessage("Hasło musi zawierać przynajmniej jedną dużą i małą literę oraz jeden specjalny znak" +
                                  ", a także nie może być krótsze niż 8 znaków");
        }
    }
}
