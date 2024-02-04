using AutoMapper;
using ECommerceApp.Application.Constants;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services.Items
{
    public class ItemService : IItemService
    {
        private readonly IMapper _mapper;
        private readonly IItemRepository _itemRepository;
        private readonly ITagRepository _tagRepository;
        private readonly IImageService _imageService;
        private readonly IBrandRepository _brandRepository;
        private readonly ITypeRepository _typeRepository;
        private readonly ICurrencyRepository _currencyRepository;

        public ItemService(IItemRepository itemRepo, IMapper mapper, ITagRepository tagRepository, IImageService imageService,
            IBrandRepository brandRepository, ITypeRepository typeRepository, ICurrencyRepository currencyRepository)
        {
            _mapper = mapper;
            _itemRepository = itemRepo;
            _tagRepository = tagRepository;
            _imageService = imageService;
            _brandRepository = brandRepository;
            _typeRepository = typeRepository;
            _currencyRepository = currencyRepository;
        }

        public ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString)
        {
            var items = _mapper.Map<List<ItemDetailsVm>>(_itemRepository.GetAllItems(pageSize, pageNo, searchString));

            var itemsList = new ListForItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Items = items,
                Count = _itemRepository.GetCountBySearchString(searchString)
            };

            return itemsList;
        }

        public List<ItemDto> GetAllItems()
        {
            return _mapper.Map<List<ItemDto>>(_itemRepository.GetAllItems());
        }

        public List<ItemInfoVm> GetItemsAddToCart()
        {
            return _mapper.Map<List<ItemInfoVm>>(_itemRepository.GetItemInfos());
        }

        public NewItemVm GetItemById(int id)
        {
            var item = _itemRepository.GetItemById(id);
            var itemVm = _mapper.Map<NewItemVm>(item);
            return itemVm;
        }

        public bool DeleteItem(int id)
        {
            return _itemRepository.DeleteItem(id);
        }

        public ItemDetailsDto GetItemDetails(int id)
        {
            var item = _itemRepository.GetItemDetailsById(id);
            if (item is null)
            {
                return null;
            }

            var dto = _mapper.Map<ItemDetailsDto>(item);
            var images = _imageService.GetImagesByItemId(dto.Id);
            dto.Images = images?.Select(i => new ImageDto
            {
                Id = i.Id,
                ImageSource = i.ImageSource,
                Name = i.Name,
                ItemId = i.ItemId
            })?.ToList() ?? new List<ImageDto>();
            return dto;
        }

        public ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            var itemsWithTags = _mapper.Map<List<ItemsTagsVm>>(_itemRepository.GetAllItemsWithTags(pageSize, pageNo, searchString));

            var itemsWithTagsList = new ListForItemWithTagsVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemTags = itemsWithTags,
                Count = _itemRepository.GetCountItemTagsBySearchString(searchString)
            };

            return itemsWithTagsList;
        }

        public bool ItemExists(int id)
        {
            var exists = _itemRepository.ExistsById(id);
            return exists;
        }

        public int AddItem(AddItemDto dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"Not accept null value {nameof(AddItemDto)}");
            }

            if (dto.Images.Count() > 5)
            {
                throw new BusinessException("Allowed only 5 images");
            }

            var tags = _tagRepository.GetTagsByIds(dto.TagsId);
            var errors = new StringBuilder(ValidTags(tags, dto.TagsId));
            errors.Append(_imageService.ValidBase64File(dto.Images?.Select(i =>
                new ValidBase64File(i.ImageName, i.ImageSource)
            )));

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            var brand = _brandRepository.GetBrandById(dto.BrandId)
                ?? throw new BusinessException($"Brand with id '{dto.BrandId}' was not found", "brandNotFound", new Dictionary<string, string> { { "id", $"{dto.BrandId}"} });
            var type = _typeRepository.GetTypeById(dto.TypeId)
                ?? throw new BusinessException($"Type with id '{dto.TypeId}' was not found", "typeNotFound", new Dictionary<string, string> { { "id", $"{dto.TypeId}"} });
            var currency = _currencyRepository.GetById(CurrencyConstants.PlnId);
            var item = new Item
            {
                Cost = dto.Cost,
                Name = dto.Name,
                Description = dto.Description,
                Brand = brand,
                BrandId = brand.Id,
                Type = type,
                TypeId = type.Id,
                Quantity = dto.Quantity,
                Currency = currency,
                CurrencyId = currency.Id,
                Warranty = dto.Warranty
            };
            item.ItemTags = tags.Select(t => new ItemTag { Item = item, Tag = t, TagId = t.Id }).ToList();
            var id = _itemRepository.AddItem(item);
            if (!dto.Images.Any())
            {
                return id;
            }

            _imageService.AddImages(new POCO.AddImagesWithBase64POCO(id, dto.Images.Select(i => new POCO.FileWithBase64Format(i.ImageName, i.ImageSource))));
            return id;
        }

        public bool UpdateItem(UpdateItemDto dto)
        {
            if (dto.Images.Count() > 5)
            {
                throw new BusinessException("Allowed only 5 images");
            }

            var item = _itemRepository.GetItemDetailsById(dto.Id);
            if (item is null)
            {
                return false;
            }

            var tags = _tagRepository.GetTagsByIds(dto.TagsId);
            var errors = new StringBuilder(ValidTags(tags, dto.TagsId));
            errors.Append(_imageService.ValidBase64File(dto.Images?.Where(i => i.ImageId == default).Select(i =>
                new ValidBase64File(i.ImageName, i.ImageSource)
            )));

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            var brand = _brandRepository.GetBrandById(dto.BrandId)
                ?? throw new BusinessException($"Brand with id '{dto.BrandId}' was not found", "brandNotFound", new Dictionary<string, string> { { "id", $"{dto.BrandId}" } });
            var type = _typeRepository.GetTypeById(dto.TypeId)
                ?? throw new BusinessException($"Type with id '{dto.TypeId}' was not found", "typeNotFound", new Dictionary<string, string> { { "id", $"{dto.TypeId}" } });
            var currency = _currencyRepository.GetById(CurrencyConstants.PlnId);

            item.Cost = dto.Cost;
            item.Name = dto.Name;
            item.Description = dto.Description;
            item.Brand = brand;
            item.BrandId = brand.Id;
            item.Type = type;
            item.TypeId = type.Id;
            item.Quantity = dto.Quantity;
            item.Currency = currency;
            item.CurrencyId = currency.Id;
            item.Warranty = dto.Warranty;
            HandleUpdateTags(item, dto.TagsId, tags);

            if (!dto.Images.Any())
            {
                _itemRepository.UpdateItem(item);
                return true;
            }

            var imgsToAdd = dto.Images.Where(i => i.ImageId == default);
            var imgsToCheck = dto.Images.Where(i => i.ImageId != default);
            var currentImages = new List<Image>(item.Images);
            var imgs = _imageService.GetImages(imgsToCheck.Select(i => i.ImageId));
            var imgErrors = ValidImages(imgsToCheck, imgs);

            if (imgErrors.Any())
            {
                throw new BusinessException(imgErrors);
            }

            foreach (var img in currentImages)
            {
                if (!imgsToCheck.Any(i => i.ImageId == img.Id))
                {
                    item.Images.Remove(img);
                }
            }

            if (imgsToAdd.Any())
            {
                _imageService.AddImages(new POCO.AddImagesWithBase64POCO(item.Id, imgsToAdd.Select(i => new POCO.FileWithBase64Format(i.ImageName, i.ImageSource))));
            }

            _itemRepository.UpdateItem(item);
            return true;
        }

        private static string ValidTags(List<Tag> tags, IEnumerable<int> tagsId)
        {
            var errors = new StringBuilder();
            foreach (var itemTagId in tagsId)
            {
                var tag = tags.FirstOrDefault(t => t.Id == itemTagId);
                if (tag is null)
                {
                    errors.Append($"Tag with id '{itemTagId}' was not found");
                }
            }

            return errors.ToString();
        }

        private static void HandleUpdateTags(Item item, IEnumerable<int> tagsId, List<Tag> tags)
        {
            var currentTags = new List<ItemTag>(item.ItemTags);

            foreach (var itemTag in currentTags)
            {
                var tagExists = tagsId.FirstOrDefault(t => t == itemTag.TagId);
                if (tagExists == default)
                {
                    item.ItemTags.Remove(itemTag);
                }
            }

            foreach (var tagId in tagsId)
            {
                if (!item.ItemTags.Any(it => it.TagId == tagId))
                {
                    item.ItemTags.Add(new ItemTag { Item = item, TagId = tagId, Tag = tags.FirstOrDefault(t => t.Id == tagId) });
                }
            }
        }

        private static string ValidImages(IEnumerable<UpdateItemImageDto> imgsToCheck, List<ImageInfoDto> imgs)
        {
            var imgErrors = new StringBuilder();

            foreach (var img in imgsToCheck)
            {
                if (!imgs.Any(i => i.Id == img.ImageId))
                {
                    imgErrors.AppendLine($"Image with id '{img.ImageId}' was not found");
                }
            }

            if (imgErrors.Length > 0)
            {
                throw new BusinessException(imgErrors.ToString());
            }

            return imgErrors.ToString();
        }
    }
}
