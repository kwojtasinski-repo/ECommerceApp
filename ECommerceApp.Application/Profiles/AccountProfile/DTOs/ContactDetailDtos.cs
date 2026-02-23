using FluentValidation;

namespace ECommerceApp.Application.Profiles.AccountProfile.DTOs
{
    public record AddContactDetailDto(
        int AccountProfileId,
        int ContactDetailTypeId,
        string Information);

    public record UpdateContactDetailDto(
        int Id,
        int AccountProfileId,
        int ContactDetailTypeId,
        string Information);

    public class AddContactDetailDtoValidator : AbstractValidator<AddContactDetailDto>
    {
        public AddContactDetailDtoValidator()
        {
            RuleFor(x => x.AccountProfileId).GreaterThan(0);
            RuleFor(x => x.ContactDetailTypeId).GreaterThan(0);
            RuleFor(x => x.Information).NotEmpty().MaximumLength(300);
        }
    }

    public class UpdateContactDetailDtoValidator : AbstractValidator<UpdateContactDetailDto>
    {
        public UpdateContactDetailDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.AccountProfileId).GreaterThan(0);
            RuleFor(x => x.ContactDetailTypeId).GreaterThan(0);
            RuleFor(x => x.Information).NotEmpty().MaximumLength(300);
        }
    }
}
