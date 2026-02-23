using FluentValidation;

namespace ECommerceApp.Application.AccountProfile.DTOs
{
    public record AddAddressDto(
        int UserProfileId,
        string Street,
        string BuildingNumber,
        int? FlatNumber,
        string ZipCode,
        string City,
        string Country);

    public record UpdateAddressDto(
        int AddressId,
        string Street,
        string BuildingNumber,
        int? FlatNumber,
        string ZipCode,
        string City,
        string Country);

    public class AddAddressDtoValidator : AbstractValidator<AddAddressDto>
    {
        public AddAddressDtoValidator()
        {
            RuleFor(x => x.UserProfileId).GreaterThan(0);
            RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
            RuleFor(x => x.BuildingNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.ZipCode).NotEmpty().MinimumLength(2).MaximumLength(12);
            RuleFor(x => x.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Country).NotEmpty().Length(2);
        }
    }

    public class UpdateAddressDtoValidator : AbstractValidator<UpdateAddressDto>
    {
        public UpdateAddressDtoValidator()
        {
            RuleFor(x => x.AddressId).GreaterThan(0);
            RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
            RuleFor(x => x.BuildingNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.ZipCode).NotEmpty().MinimumLength(2).MaximumLength(12);
            RuleFor(x => x.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Country).NotEmpty().Length(2);
        }
    }
}
