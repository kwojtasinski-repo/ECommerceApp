using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class AddressDto : IMapFrom<Address>
    {
        public int? Id { get; set; }
        public string Street { get; set; }
        public string BuildingNumber { get; set; }
        public int? FlatNumber { get; set; }
        public string ZipCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public int CustomerId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<AddressDto, Address>()
                .ForMember(a => a.ZipCode, a => a.MapFrom(ad => MapToZipCode(ad.ZipCode)))
                .ReverseMap();
        }

        private static int MapToZipCode(string zipCode)
        {
            int.TryParse(zipCode.Replace("-", zipCode), out var zipIntNumber);
            return zipIntNumber;
        }
    }

    public class AddressDtoValidation : AbstractValidator<AddressDto>
    {
        public AddressDtoValidation()
        {
            When(x => x.Id is not null,
                () => RuleFor(x => x.FlatNumber).GreaterThan(0));
            RuleFor(x => x.Street).NotNull().MinimumLength(2).MaximumLength(255);
            RuleFor(x => x.BuildingNumber).NotNull().MinimumLength(1).MaximumLength(100);
            When(x => x.FlatNumber is not null && x.FlatNumber.Value <= 0,
                () => RuleFor(x => x.FlatNumber).GreaterThan(0));
            RuleFor(x => x.ZipCode).NotNull().Length(5).Matches("\\d{2}-\\d{3}");
            RuleFor(x => x.City).NotNull().MinimumLength(1).MaximumLength(255);
            RuleFor(x => x.Country).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
