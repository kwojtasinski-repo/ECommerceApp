using ECommerceApp.Application.Services.Items;
using Moq;
using Xunit;
using FluentAssertions;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.UnitTests.Common;
using ECommerceApp.Application.DTO;
using System.Collections.Generic;
using ECommerceApp.Domain.Model;
using System.Linq;
using ECommerceApp.Application.POCO;

namespace ECommerceApp.Tests.Services.Item
{
    public class ItemServiceTests : BaseTest
    {
        private readonly Mock<IItemRepository> _itemRepository;
        private readonly Mock<ITagRepository> _tagRepository;
        private readonly Mock<IImageService> _imageService;
        private readonly Mock<IBrandRepository> _brandRepository;
        private readonly Mock<ITypeRepository> _typeRepository;
        private readonly Mock<ICurrencyRepository> _currencyRepository;

        public ItemServiceTests()
        {
            _itemRepository = new Mock<IItemRepository>();
            _tagRepository = new Mock<ITagRepository>();
            _imageService = new Mock<IImageService>();
            _brandRepository = new Mock<IBrandRepository>();
            _typeRepository = new Mock<ITypeRepository>();
            _currencyRepository = new Mock<ICurrencyRepository>();
            InitSetup();
        }

        public ItemService CreateItemService()
            => new (_itemRepository.Object, _mapper, _tagRepository.Object, _imageService.Object,
                _brandRepository.Object, _typeRepository.Object, _currencyRepository.Object);

        [Fact]
        public void given_valid_item_id_should_exists()
        {
            int id = 1;
            _itemRepository.Setup(i => i.ItemExists(id)).Returns(true);
            var itemService = CreateItemService();

            var exists = itemService.ItemExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_item_id_shouldnt_exists()
        {
            int id = 1;
            var itemService = CreateItemService();

            var exists = itemService.ItemExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_item_when_add_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();

            var action = () => itemService.AddItem((AddItemDto) null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Not accept null value");
        }

        [Fact]
        public void given_item_with_more_than_5_images_when_add_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                Images = new List<AddItemImageDto>
                {
                    new AddItemImageDto("abc", ""),
                    new AddItemImageDto("abc", ""),
                    new AddItemImageDto("abc", ""),
                    new AddItemImageDto("abc", ""),
                    new AddItemImageDto("abc", ""),
                    new AddItemImageDto("abc", ""),
                    new AddItemImageDto("abc", "")
                }
            };

            var action = () => itemService.AddItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Allowed only 5 images");
        }

        [Fact]
        public void given_item_with_not_existing_tags_when_add_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                TagsId = new List<int> { 1, 2, 3 }
            };

            var action = () => itemService.AddItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("was not found");
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains('1');
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains('2');
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains('3');
        }

        [Fact]
        public void given_item_with_invalid_image_base64_format_when_add_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                Images = new List<AddItemImageDto>
                {
                    new AddItemImageDto("abc", "12312412fsddvzxcvs"),
                }
            };

            var action = () => itemService.AddItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("has invalid Base64 string");
        }

        [Fact]
        public void given_item_with_not_existing_brand_when_add_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                BrandId = 100
            };

            var action = () => itemService.AddItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Brand with id '{dto.BrandId}' was not found");
        }

        [Fact]
        public void given_item_with_not_existing_type_when_add_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                BrandId = AddDefaultBrand(),
                TypeId = 100
            };

            var action = () => itemService.AddItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Type with id '{dto.TypeId}' was not found");
        }

        [Fact]
        public void given_valid_item_with_with_images_when_add_item_dto_should_add()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                BrandId = AddDefaultBrand(),
                TypeId = AddDefaultType(),
                Cost = 2000M,
                Name = nameof(AddItemDto),
                Description = nameof(AddItemDto),
                Quantity = 1,
                Warranty = "100",
                Images = new List<AddItemImageDto>
                {
                    new AddItemImageDto("img1.png", "SW1hZ2VTb3VyY2U=")
                }
            };

            itemService.AddItem(dto);

            _itemRepository.Verify(i => i.AddItem(It.IsAny<Domain.Model.Item>()), Times.Once);
            _imageService.Verify(i => i.AddImages(It.IsAny<AddImagesWithBase64POCO>()), Times.Once);
        }

        [Fact]
        public void given_valid_item_with_without_images_when_add_item_dto_should_add()
        {
            var itemService = CreateItemService();
            var dto = new AddItemDto
            {
                BrandId = AddDefaultBrand(),
                TypeId = AddDefaultType(),
                Cost = 2000M,
                Name = nameof(AddItemDto),
                Description = nameof(AddItemDto),
                Quantity = 1,
                Warranty = "100",
                Images = new List<AddItemImageDto>()
            };

            itemService.AddItem(dto);

            _itemRepository.Verify(i => i.AddItem(It.IsAny<Domain.Model.Item>()), Times.Once);
            _imageService.Verify(i => i.AddImages(It.IsAny<AddImagesPOCO>()), Times.Never);
        }

        [Fact]
        public void given_item_with_more_than_5_images_when_update_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new UpdateItemDto
            {
                Images = new List<UpdateItemImageDto>
                {
                    new UpdateItemImageDto(0, "abc", ""),
                    new UpdateItemImageDto(0, "abc", ""),
                    new UpdateItemImageDto(0, "abc", ""),
                    new UpdateItemImageDto(0, "abc", ""),
                    new UpdateItemImageDto(0, "abc", ""),
                    new UpdateItemImageDto(0, "abc", ""),
                    new UpdateItemImageDto(0, "abc", "")
                }
            };

            var action = () => itemService.UpdateItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Allowed only 5 images");
        }

        [Fact]
        public void given_not_existing_item_when_update_item_dto_show_throw_an_exception()
        {
            var itemService = CreateItemService();
            var dto = new UpdateItemDto
            {
                Id = 100
            };

            var action = () => itemService.UpdateItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Item with id '{dto.Id}' was not found");
        }

        [Fact]
        public void given_item_with_not_existing_brand_when_update_item_dto_should_throw_an_exception()
        {
            var itemService = CreateItemService();
            var id = AddDefaultItem();
            var dto = new UpdateItemDto
            {
                Id = id,
                BrandId = 100
            };

            var action = () => itemService.UpdateItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Brand with id '{dto.BrandId}' was not found");
        }

        [Fact]
        public void given_item_with_not_existing_type_when_update_item_dto_should_throw_an_exception()
        {
            var itemService = CreateItemService();
            var id = AddDefaultItem();
            var dto = new UpdateItemDto
            {
                Id = id,
                BrandId = AddDefaultBrand(),
                TypeId = 100
            };

            var action = () => itemService.UpdateItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Type with id '{dto.TypeId}' was not found");
        }

        [Fact]
        public void given_item_with_not_existing_tags_when_update_item_dto_should_throw_an_exception()
        {
            var itemService = CreateItemService();
            var id = AddDefaultItem();
            var dto = new UpdateItemDto
            {
                Id = id,
                TagsId = new List<int> { 1, 2, 3 }
            };

            var action = () => itemService.UpdateItem(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("was not found");
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains('1');
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains('2');
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains('3');
        }

        [Fact]
        public void given_valid_item_without_images_when_update_item_dto_should_update()
        {
            var itemService = CreateItemService();
            var id = AddDefaultItem();
            AddDefaultBrand();
            AddDefaultType();
            var dto = new UpdateItemDto
            {
                Id = id,
                Name = "Name",
                Cost = 100M,
                Description = "Description",
                BrandId = 1,
                TypeId = 1,
                Quantity = 100,
                Warranty = "200",
            };

            itemService.UpdateItem(dto);

            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_item_with_not_existing_image_when_update_item_dto_should_throw_an_exception()
        {
            var itemService = CreateItemService();
            var id = AddDefaultItem();
            AddDefaultBrand();
            AddDefaultType();
            var dto = new UpdateItemDto
            {
                Id = id,
                Name = "Name",
                Cost = 100M,
                Description = "Description",
                BrandId = 1,
                TypeId = 1,
                Quantity = 100,
                Warranty = "200",
                Images = new List<UpdateItemImageDto>
                {
                    new UpdateItemImageDto(AddDefaultImage(id), null, null),
                    new UpdateItemImageDto(0, "img1.png", "SW1hZ2VTb3VyY2U="),
                    new UpdateItemImageDto(0, "img1.png", "SW1hZ2VTb3VyY2U=")
                }
            };

            itemService.UpdateItem(dto);

            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Once);
            _imageService.Verify(i => i.AddImages(It.IsAny<AddImagesWithBase64POCO>()), Times.Once);
        }

        [Fact]
        public void given_item_with_images_when_update_item_dto_should_update()
        {
            var itemService = CreateItemService();
            var id = AddDefaultItem();
            AddDefaultBrand();
            AddDefaultType();
            var dto = new UpdateItemDto
            {
                Id = id,
                Name = "Name",
                Cost = 100M,
                Description = "Description",
                BrandId = 1,
                TypeId = 1,
                Quantity = 100,
                Warranty = "200",
                Images = new List<UpdateItemImageDto>
                {
                    new UpdateItemImageDto(100, null, null)
                }
            };

            var action = () => itemService.UpdateItem(dto);

            action.Should().Throw<BusinessException>().Which.Message.Should().Contain($"Image with id '100' was not found");
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Never);
            _imageService.Verify(i => i.AddImages(It.IsAny<AddImagesPOCO>()), Times.Never);
        }

        private int AddDefaultItem()
        {
            var item = new Domain.Model.Item
            {
                Id = 1,
                Cost = 1M,
                Name = "Name",
                Description = "Description",
                Quantity = 1,
                Warranty = "20",
                BrandId = 1,
                TypeId = 1,
                CurrencyId = 1,
                ItemTags = new List<ItemTag>(),
                Images = new List<Domain.Model.Image>()
            };
            _itemRepository.Setup(i => i.GetItemById(item.Id)).Returns(item);
            _itemRepository.Setup(i => i.GetItemDetailsById(item.Id)).Returns(item);
            return item.Id;
        }

        private int AddDefaultBrand()
        {
            var id = 1;
            var brand = new Brand
            {
                Id = id,
                Name = "Test",
            };
            _brandRepository.Setup(b => b.ExistsBrand(id)).Returns(true);
            _brandRepository.Setup(b => b.GetBrandById(id)).Returns(brand);
            return id;
        }

        private int AddDefaultImage(int? itemId = null)
        {
            var id = 1;
            var image = new Domain.Model.Image
            {
                Id = id,
                ItemId = itemId ?? id,
                Name = "Test",
                SourcePath = "Path",
            };
            _imageService.Setup(i => i.GetImages(It.IsAny<IEnumerable<int>>())).Returns(new List<ImageInfoDto>
            {
                new ImageInfoDto { Id = id, Name = "Test", ItemId = itemId ?? id }
            });
            _imageService.Setup(i => i.GetImagesByItemId(itemId ?? id)).Returns(new List<Application.ViewModels.Image.GetImageVm>
            {
                new Application.ViewModels.Image.GetImageVm { Id = id }
            });
            return id;
        }

        private int AddDefaultType()
        {
            var id = 1;
            var type = new Domain.Model.Type
            {
                Id = id,
                Name = "Test",
            };
            _typeRepository.Setup(t => t.GetTypeById(id)).Returns(type);
            return id;
        }

        private void InitSetup()
        {
            _itemRepository.Setup(i => i.GetAllItems()).Returns(new List<Domain.Model.Item>().AsQueryable());
            _itemRepository.Setup(i => i.GetAllItems()).Returns(new List<Domain.Model.Item>().AsQueryable());
            _itemRepository.Setup(i => i.GetAllItemsWithTags()).Returns(new List<ItemTag>().AsQueryable());
            _tagRepository.Setup(t => t.GetTagsByIds(It.IsAny<IEnumerable<int>>())).Returns(new List<Tag>());
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency>());
            _brandRepository.Setup(b => b.GetAllBrands()).Returns(new List<Brand>());
            _tagRepository.Setup(t => t.GetAllTags()).Returns(new List<Tag>());
            _typeRepository.Setup(t => t.GetAllTypes()).Returns(new List<Domain.Model.Type>());
            _imageService.Setup(i => i.GetImages(It.IsAny<IEnumerable<int>>())).Returns(new List<ImageInfoDto>());
            _imageService.Setup(i => i.GetImagesByItemId(It.IsAny<int>())).Returns(new List<Application.ViewModels.Image.GetImageVm>());
            _currencyRepository.Setup(c => c.GetById(1)).Returns(new Currency
            {
                Id = 1,
                Code = "PLN",
                Description = "Polski złoty",
            });
        }
    }
}
