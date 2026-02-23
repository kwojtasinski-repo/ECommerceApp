using FluentValidation;

namespace ECommerceApp.Application.AccountProfile.DTOs
{
    public record AddAddressDto(
        int UserProfileId,
        string Street,
        string BuildingNumber,
        int? FlatNumber,
        int ZipCode,
        string City,
        string Country);

    public record UpdateAddressDto(
        int AddressId,
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
            RuleFor(x => x.UserProfileId).GreaterThan(0);
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
            RuleFor(x => x.AddressId).GreaterThan(0);
            RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
            RuleFor(x => x.BuildingNumber).NotEmpty().MaximumLength(150);
            RuleFor(x => x.ZipCode).GreaterThan(0);
            RuleFor(x => x.City).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Country).NotEmpty().MaximumLength(300);
        }
    }
}
