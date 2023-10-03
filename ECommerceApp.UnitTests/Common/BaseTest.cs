using AutoMapper;
using AutoMapper.Internal;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.UnitTests.Common
{
    public class BaseTest
    {
        protected readonly IMapper _mapper;

        public BaseTest()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
                cfg.Internal().MethodMappingEnabled = false;
            });

            _mapper = configurationProvider.CreateMapper();
        }
    }
}
