﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
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

        public int Add(ItemVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{vm.GetType().Name} cannot be null");
            }

            if (vm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var item = vm.MapToItem();
            var id = _itemRepository.Add(item);
            return id;
        }

        public void Update(ItemVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{vm.GetType().Name} cannot be null");
            }

            var item = vm.MapToItem();
            _itemRepository.Update(item);
        }


        public int AddItem(NewItemVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewItemVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var tags = _tagRepository.GetTagsByIds(model.ItemTags.Select(it => it.TagId));
            var errors = new StringBuilder();
            foreach(var itemTag in model.ItemTags)
            {
                var tag = tags.FirstOrDefault(t => t.Id == itemTag.TagId);
                if (tag is null)
                {
                    errors.Append($"Tag with id '{itemTag.TagId}' was not found");
                }                    
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            var item = _mapper.Map<Item>(model);
            var id = _itemRepository.AddItem(item);
            return id;
        }

        public ListForItemVm GetAllItemsForList(int pageSize, int pageNo, string searchString)
        {
            var items = _itemRepository.GetAllItems()
                .Where(i => i.Name.StartsWith(searchString))
                .Skip(pageSize * (pageNo - 1)).Take(pageSize);
            var itemsToShow = items.ProjectTo<ItemDetailsVm>(_mapper.ConfigurationProvider).ToList();

            var itemsList = new ListForItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Items = itemsToShow,
                // TODO: Think about performance
                Count = _itemRepository.GetAll().Count()
            };

            return itemsList;
        }

        public List<ItemDto> GetAllItems()
        {
            var items = _itemRepository.GetAllItems()
                .ProjectTo<ItemDto>(_mapper.ConfigurationProvider)
                .ToList();
            return items;
        }

        public List<ItemInfoVm> GetItemsAddToCart()
        {
            var items = _itemRepository.GetAll();
            var itemsVm = items.ProjectTo<ItemInfoVm>(_mapper.ConfigurationProvider).ToList();
            return itemsVm;
        }

        public NewItemVm GetItemById(int id)
        {
            var item = _itemRepository.GetItemById(id);
            var itemVm = _mapper.Map<NewItemVm>(item);
            return itemVm;
        }

        public void UpdateItem(NewItemVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewItemVm).Name} cannot be null");
            }

            var item = _itemRepository.GetItemById(model.Id);
            item.Name = model.Name;
            item.Cost = model.Cost;
            item.Warranty = model.Warranty;
            item.Quantity = model.Quantity;
            item.Description = model.Description;
            item.BrandId = model.BrandId;
            item.TypeId = model.TypeId;
            item.CurrencyId = model.CurrencyId;
            var currentTags = new List<ItemTag>(item.ItemTags);
            foreach (var itemTag in currentTags)
            {
                var itemTagExists = model.ItemTags.FirstOrDefault(it => it.TagId == itemTag.TagId);
                if (itemTagExists is null)
                {
                    item.ItemTags.Remove(itemTag);
                }
            }
            var tags = _tagRepository.GetTagsByIds(model.ItemTags.Select(it => it.TagId));

            foreach (var itemTag in model.ItemTags)
            {
                var itemTagExists = item.ItemTags.FirstOrDefault(it => it.TagId == itemTag.TagId);
                var tag = tags.FirstOrDefault(t => t.Id == itemTag.TagId) 
                    ?? throw new BusinessException($"Tag with id '{itemTag.TagId}' was not found");
                if (itemTagExists is null)
                {
                    var itemTagToAdd = new ItemTag { Item = item, ItemId = itemTag.ItemId, TagId = itemTag.TagId, Tag = tag };
                    item.ItemTags.Add(itemTagToAdd);
                    tag.ItemTags.Add(itemTagToAdd);
                }
            }

            _itemRepository.UpdateItem(item);
        }

        public void DeleteItem(int id)
        {
            _itemRepository.Delete(id);
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
                ItemId = i.ItemId
            })?.ToList() ?? new List<ImageDto>();
            return dto;
        }

        public ListForItemWithTagsVm GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            var itemsWithTags = _itemRepository.GetAllItemsWithTags()//.Where(it => it.Item.Name.StartsWith(searchString))
                .ProjectTo<ItemsTagsVm>(_mapper.ConfigurationProvider)
                .ToList();
            var itemsWithTagsToShow = itemsWithTags.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemsWithTagsList = new ListForItemWithTagsVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemTags = itemsWithTagsToShow,
                Count = itemsWithTags.Count
            };

            return itemsWithTagsList;
        }

        public bool ItemExists(int id)
        {
            var exists = _itemRepository.ItemExists(id);
            return exists;
        }

        public int AddItem(AddItemDto dto)
        {
            if (dto.Images.Count() > 5)
            {
                throw new BusinessException("Allowed only 5 images");
            }

            var tags = _tagRepository.GetTagsByIds(dto.TagsId);
            var errors = new StringBuilder();
            foreach (var itemTagId in dto.TagsId)
            {
                var tag = tags.FirstOrDefault(t => t.Id == itemTagId);
                if (tag is null)
                {
                    errors.Append($"Tag with id '{itemTagId}' was not found");
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            var brand = _brandRepository.GetById(dto.BrandId)
                ?? throw new BusinessException($"Brand with id '{dto.BrandId}' was not found");
            var type = _typeRepository.GetById(dto.TypeId)
                ?? throw new BusinessException($"Type with id '{dto.TypeId}' was not found");
            var currency = _currencyRepository.GetById(dto.CurrencyId)
                ?? throw new BusinessException($"Currency with id '{dto.CurrencyId}' was not found");
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

            var addImages = new POCO.AddImagesPOCO();
            foreach (var addImg in dto.Images)
            {
                if (addImg is null || string.IsNullOrWhiteSpace(addImg.ImageSource))
                {
                    continue;
                }

                var bytes = Convert.FromBase64String(addImg.ImageSource);
                addImages.Files.Add(new FormFile(new MemoryStream(bytes), 0, bytes.Length, addImg.ImageName, addImg.ImageName));
                addImages.ItemId = item.Id;
            }

            if (addImages.Files.Any())
            {
                _imageService.AddImages(addImages);
            }

            return id;
        }

        public void UpdateItem(UpdateItemDto dto)
        {
            if (dto.Images.Count() > 5)
            {
                throw new BusinessException("Allowed only 5 images");
            }

            var item = _itemRepository.GetItemDetailsById(dto.Id)
                ?? throw new BusinessException($"Item with id '{dto.Id}' was not found");
            var tags = _tagRepository.GetTagsByIds(dto.TagsId);
            var errors = new StringBuilder();
            foreach (var tagId in dto.TagsId)
            {
                var tag = tags.FirstOrDefault(t => t.Id == tagId);
                if (tag is null)
                {
                    errors.Append($"Tag with id '{tagId}' was not found");
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            var brand = _brandRepository.GetById(dto.BrandId)
                ?? throw new BusinessException($"Brand with id '{dto.BrandId}' was not found");
            var type = _typeRepository.GetById(dto.TypeId)
                ?? throw new BusinessException($"Type with id '{dto.TypeId}' was not found");
            var currency = _currencyRepository.GetById(dto.CurrencyId)
                ?? throw new BusinessException($"Currency with id '{dto.CurrencyId}' was not found");

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
            var currentTags = new List<ItemTag>(item.ItemTags);
            
            foreach (var itemTag in currentTags)
            {
                var tagExists = dto.TagsId.FirstOrDefault(t => t == itemTag.TagId);
                if (tagExists == default)
                {
                    item.ItemTags.Remove(itemTag);
                }
            }

            foreach(var tagId in dto.TagsId)
            {
                if (!item.ItemTags.Any(it => it.TagId == tagId))
                {
                    item.ItemTags.Add(new ItemTag { Item = item, TagId = tagId, Tag = tags.FirstOrDefault(t => t.Id == tagId) });
                }
            }

            if (!dto.Images.Any())
            {
                _itemRepository.UpdateItem(item);
                return;
            }

            var imgToAdd = dto.Images.Where(i => i.ImageId == default);
            var imgToCheck = dto.Images.Where(i => i.ImageId != default);
            var currentImages = new List<Image>(item.Images);
            var imgs = _imageService.GetImages(imgToCheck.Select(i => i.ImageId));
            var imgErrors = new StringBuilder();

            foreach (var img in imgs)
            {
                if (!imgToCheck.Any(i => i.ImageId == img.Id))
                {
                    imgErrors.AppendLine($"Image with id '{img.Id}' was not found");
                }
            }

            if (imgErrors.Length > 0)
            {
                throw new BusinessException(imgErrors.ToString());
            }

            foreach (var img in currentImages)
            {
                if (!imgToCheck.Any(i => i.ImageId == img.Id))
                {
                    item.Images.Remove(img);
                }
            }

            if (imgToAdd.Any())
            {
                var addImages = new POCO.AddImagesPOCO();
                foreach (var addImg in imgToAdd)
                {
                    if (addImg is null || string.IsNullOrWhiteSpace(addImg.ImageSource))
                    {
                        continue;
                    }

                    var bytes = Convert.FromBase64String(addImg.ImageSource);
                    addImages.Files.Add(new FormFile(new MemoryStream(bytes), 0, bytes.Length, addImg.ImageName, addImg.ImageName));
                    addImages.ItemId = item.Id;
                }
                
                if (addImages.Files.Any())
                {
                    _imageService.AddImages(addImages);
                }
            }

            _itemRepository.UpdateItem(item);
        }
    }
}
