using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace ECommerceApp.Application.ViewModels.User
{
    public class RoleVm : IMapFrom<IdentityRole>
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<IdentityRole, RoleVm>();
        }
    }

    public class RoleValidation : AbstractValidator<RoleVm>
    {
        public RoleValidation()
        {
            RuleFor(x => x.Name).NotNull().WithMessage("Nazwa roli nie może być pusta")
                                    .Length(3,100).WithMessage("Nazwa roli może zawierać minimalnie 3 znaki a maksymalnie 100");
        }
    }
}
