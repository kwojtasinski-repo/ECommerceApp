using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Domain.Interface;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Type
{
    public class TypeServiceTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<ITypeRepository> _typeRepository;
        public TypeServiceTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
            _typeRepository = new Mock<ITypeRepository>();
        }

        [Fact]
        public void given_valid_type_should_add()
        {
            var tag = CreateTypeVm(0);
            var typeService = new TypeService(_typeRepository.Object, _mapper);

            typeService.AddType(tag);

            _typeRepository.Verify(t => t.AddType(It.IsAny<Domain.Model.Type>()), Times.Once);
        }

        [Fact]
        public void given_invalid_type_should_throw_an_exception()
        {
            var type = CreateTypeVm(1);
            var tagService = new TypeService(_typeRepository.Object, _mapper);

            Action action = () => tagService.AddType(type);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_id_type_should_exists()
        {
            var id = 1;
            var type = CreateType(id);
            _typeRepository.Setup(t => t.GetById(id)).Returns(type);
            var typeService = new TypeService(_typeRepository.Object, _mapper);

            var exists = typeService.TypeExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_id_type_shouldnt_exists()
        {
            var id = 1;
            var typeService = new TypeService(_typeRepository.Object, _mapper);

            var exists = typeService.TypeExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_type_should_update()
        {
            var tag = CreateTypeVm(1);
            var typeService = new TypeService(_typeRepository.Object, _mapper);

            typeService.UpdateType(tag);

            _typeRepository.Verify(t => t.UpdateType(It.IsAny<Domain.Model.Type>()), Times.Once);
        }

        [Fact]
        public void given_null_type_shouldnt_update()
        {
            var tagService = new TypeService(_typeRepository.Object, _mapper);

            tagService.UpdateType(null);

            _typeRepository.Verify(t => t.UpdateType(It.IsAny<Domain.Model.Type>()), Times.Never);
        }

        private TypeVm CreateTypeVm(int id)
        {
            var type = new TypeVm
            {
                Id = id,
                Name = "Type"
            };
            return type;
        }

        private Domain.Model.Type CreateType(int id)
        {
            var type = new Domain.Model.Type
            {
                Id = id,
                Name = "Type"
            };
            return type;
        }
    }
}
