using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Tag
{
    public class TagServiceTests : BaseTest
    {
        private readonly Mock<ITagRepository> _tagRepository;

        public TagServiceTests()
        {
            _tagRepository = new Mock<ITagRepository>();
        }

        [Fact]
        public void given_valid_tag_should_add()
        {
            var tag = CreateTagVm(0);
            var tagService = new TagService(_tagRepository.Object, _mapper);

            tagService.AddTag(tag);

            _tagRepository.Verify(t => t.AddTag(It.IsAny<Domain.Model.Tag>()), Times.Once);
        }

        [Fact]
        public void given_invalid_tag_should_throw_an_exception()
        {
            var tag = CreateTagVm(1);
            var tagService = new TagService(_tagRepository.Object, _mapper);

            Action action = () => tagService.AddTag(tag);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_id_tag_should_exists()
        {
            var id = 1;
            var tag = CreateTag(id);
            _tagRepository.Setup(t => t.GetById(id)).Returns(tag);
            var tagService = new TagService(_tagRepository.Object, _mapper);

            var exists = tagService.TagExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_id_tag_shouldnt_exists()
        {
            var id = 1;
            var tagService = new TagService(_tagRepository.Object, _mapper);

            var exists = tagService.TagExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_tag_should_update()
        {
            var tag = CreateTagVm(1);
            var tagService = new TagService(_tagRepository.Object, _mapper);
            
            tagService.UpdateTag(tag);

            _tagRepository.Verify(t => t.UpdateTag(It.IsAny<Domain.Model.Tag>()), Times.Once);
        }

        [Fact]
        public void given_null_tag_when_add_should_throw_an_exception()
        {
            var tagService = new TagService(_tagRepository.Object, _mapper);

            Action action = () => tagService.AddTag(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_tag_when_update_should_throw_an_exception()
        {
            var tagService = new TagService(_tagRepository.Object, _mapper);

            Action action = () => tagService.UpdateTag(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private TagVm CreateTagVm(int id)
        {
            var tag = new TagVm
            {
                Id = id,
                Name = "Tag"
            };
            return tag;
        }
        
        private Domain.Model.Tag CreateTag(int id)
        {
            var tag = new Domain.Model.Tag
            {
                Id = id,
                Name = "Tag"
            };
            return tag;
        }
    }
}
