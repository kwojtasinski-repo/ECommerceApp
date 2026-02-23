using FluentValidation;

namespace ECommerceApp.Application.Profiles.AccountProfile.DTOs
{
    public record AddAddressDto(
        int AccountProfileId,
        string Street,
        string BuildingNumber,
        int? FlatNumber,
        int ZipCode,
        string City,
        string Country);

    public record UpdateAddressDto(
        int Id,
        int AccountProfileId,
        string Street,
        string BuildingNumber,
        int? FlatNumber,
        int ZipCode,
        string City,
        string Country);

    public class AddAddressDtoValidator : AbstractValidator<AddAddressDto>
    {
        public AddAddressDtoValidator()
        {
            RuleFor(x => x.AccountProfileId).GreaterThan(0);
            RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
            RuleFor(x => x.BuildingNumber).NotEmpty().MaximumLength(150);
            RuleFor(x => x.ZipCode).GreaterThan(0);
            RuleFor(x => x.City).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Country).NotEmpty().MaximumLength(300);
        }
    }

    public class UpdateAddressDtoValidator : AbstractValidator<UpdateAddressDto>
    {
        public UpdateAddressDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.AccountProfileId).GreaterThan(0);
            RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
            RuleFor(x => x.BuildingNumber).NotEmpty().MaximumLength(150);
            RuleFor(x => x.ZipCode).GreaterThan(0);
            RuleFor(x => x.City).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Country).NotEmpty().MaximumLength(300);
        }
    }
}
