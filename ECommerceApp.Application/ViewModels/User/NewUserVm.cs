﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.ViewModels.User
{
    public class NewUserVm : IMapFrom<ApplicationUser>
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public List<string> UserRoles { get; set; } = new List<string>();
        public List<RoleVm> Roles { get; set; } = new List<RoleVm>();

        public string PasswordToChange { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ApplicationUser, NewUserVm>();
        }
    }
    public class NewUserValidation : AbstractValidator<NewUserVm>
    {
        public NewUserValidation()
        {
            RuleFor(x => x.UserName).NotNull().WithMessage("Nazwa użytkownika nie może być pusta")
                                    .EmailAddress().WithMessage("Proszę podać email");
            RuleFor(x => x.Email).NotNull().WithMessage("Email nie może być pusty")
                                 .EmailAddress().WithMessage("Proszę podać email");
            When(x => x.UserRoles is not null && x.UserRoles.Any(), () =>
            {
                RuleForEach(x => x.UserRoles).NotNull().NotEmpty();
            });
        }
    }
}
