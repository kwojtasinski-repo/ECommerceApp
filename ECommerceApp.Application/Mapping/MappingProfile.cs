using AutoMapper;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.AccountProfile.ValueObjects;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.Domain.Supporting.Currencies.ValueObjects;
using System;
using System.Linq;
using System.Reflection;

namespace ECommerceApp.Application.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Email, string>().ConvertUsing(x => x.Value);
            CreateMap<PhoneNumber, string>().ConvertUsing(x => x.Value);
            CreateMap<Nip, string>().ConvertUsing((src, dest, ctx) => src == null ? null! : src.Value);
            CreateMap<CompanyName, string>().ConvertUsing((src, dest, ctx) => src == null ? null! : src.Value);
            CreateMap<UserProfileId, int>().ConvertUsing(x => x.Value);
            CreateMap<AddressId, int>().ConvertUsing(x => x.Value);
            CreateMap<Street, string>().ConvertUsing(x => x.Value);
            CreateMap<BuildingNumber, string>().ConvertUsing(x => x.Value);
            CreateMap<FlatNumber, int>().ConvertUsing(x => x.Value);
            CreateMap<ZipCode, string>().ConvertUsing(x => x.Value);
            CreateMap<City, string>().ConvertUsing(x => x.Value);
            CreateMap<Country, string>().ConvertUsing(x => x.Value);

            CreateMap<ProductId, int>().ConvertUsing(x => x.Value);
            CreateMap<CategoryId, int>().ConvertUsing(x => x.Value);
            CreateMap<TagId, int>().ConvertUsing(x => x.Value);
            CreateMap<ImageId, int>().ConvertUsing(x => x.Value);
            CreateMap<ProductName, string>().ConvertUsing(x => x.Value);
            CreateMap<Slug, string>().ConvertUsing(x => x.Value);
            CreateMap<CategorySlug, string>().ConvertUsing(x => x.Value);
            CreateMap<TagSlug, string>().ConvertUsing(x => x.Value);
            CreateMap<Price, decimal>().ConvertUsing(x => x.Amount);
            CreateMap<TagName, string>().ConvertUsing(x => x.Value);
            CreateMap<CategoryName, string>().ConvertUsing(x => x.Value);
            CreateMap<ProductDescription, string>().ConvertUsing(x => x.Value);
            CreateMap<ProductQuantity, int>().ConvertUsing(x => x.Value);
            CreateMap<ImageFileName, string>().ConvertUsing(x => x.Value);

            CreateMap<CurrencyId, int>().ConvertUsing(x => x.Value);
            CreateMap<CurrencyRateId, int>().ConvertUsing(x => x.Value);
            CreateMap<CurrencyCode, string>().ConvertUsing(x => x.Value);
            CreateMap<CurrencyDescription, string>().ConvertUsing(x => x.Value);

            ApplyMappingFromAssembly(Assembly.GetExecutingAssembly());
        }

        private void ApplyMappingFromAssembly(Assembly assembly)
        {
            var types = assembly.GetExportedTypes()
                .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>)))
                .ToList();

            foreach(var type in types)
            {
                var instance = Activator.CreateInstance(type);
                var methodInfo = type.GetMethod("Mapping");
                methodInfo?.Invoke(instance, new object[] { this });
            }
        }
    }
}
