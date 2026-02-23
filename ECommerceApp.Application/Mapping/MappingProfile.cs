using AutoMapper;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.AccountProfile.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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
