using ECommerceApp.Domain.Catalog.Products.Events;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Item
    {
        public ItemId Id { get; private set; } = new ItemId(0);
        public ProductName Name { get; private set; } = default!;
        public Price Cost { get; private set; } = default!;
        public ProductQuantity Quantity { get; private set; } = default!;
        public ProductDescription Description { get; private set; } = default!;
        public ProductStatus Status { get; private set; }
        public CategoryId CategoryId { get; private set; } = default!;

        private readonly List<Image> _images = new();
        public IReadOnlyList<Image> Images => _images.AsReadOnly();

        private readonly List<ItemTag> _itemTags = new();
        public IReadOnlyList<ItemTag> ItemTags => _itemTags.AsReadOnly();

        private Item() { }

        public static (Item Item, ProductCreated Event) Create(
            string name,
            decimal cost,
            int quantity,
            string description,
            int categoryId)
        {
            if (quantity < 0)
                throw new DomainException("Quantity must not be negative.");
            if (categoryId <= 0)
                throw new DomainException("CategoryId must be positive.");

            var item = new Item
            {
                Name = new ProductName(name),
                Cost = new Price(cost),
                Quantity = new ProductQuantity(quantity),
                Description = new ProductDescription(description),
                Status = ProductStatus.Draft,
                CategoryId = new CategoryId(categoryId)
            };

            var @event = new ProductCreated(item.Id.Value, item.Name.Value, DateTime.UtcNow);
            return (item, @event);
        }

        public ProductPublished Publish()
        {
            if (Status == ProductStatus.Published)
                throw new DomainException("Product is already published.");
            Status = ProductStatus.Published;
            return new ProductPublished(Id.Value, DateTime.UtcNow);
        }

        public ProductUnpublished Unpublish()
        {
            if (Status != ProductStatus.Published)
                throw new DomainException("Only published products can be unpublished.");
            Status = ProductStatus.Unpublished;
            return new ProductUnpublished(Id.Value, DateTime.UtcNow);
        }

        public void DecreaseQuantity(int amount)
        {
            if (amount <= 0)
                throw new DomainException("Decrease amount must be positive.");
            if (Quantity.Value - amount < 0)
                throw new DomainException($"Cannot decrease quantity by {amount}. Available: {Quantity.Value}.");
            Quantity = new ProductQuantity(Quantity.Value - amount);
        }

        public void IncreaseQuantity(int amount)
        {
            if (amount <= 0)
                throw new DomainException("Increase amount must be positive.");
            Quantity = new ProductQuantity(Quantity.Value + amount);
        }

        public void UpdateDetails(string name, decimal cost, string description, int categoryId)
        {
            if (categoryId <= 0)
                throw new DomainException("CategoryId must be positive.");

            Name = new ProductName(name);
            Cost = new Price(cost);
            Description = new ProductDescription(description);
            CategoryId = new CategoryId(categoryId);
        }

        public void AddImage(string fileName, bool isMain, int sortOrder)
        {
            if (_images.Count >= 5)
                throw new DomainException("A product can have at most 5 images.");

            if (isMain)
            {
                foreach (var existing in _images)
                {
                    existing.UnsetMain();
                }
            }

            _images.Add(Image.Create(Id, fileName, isMain, sortOrder));
        }

        public bool RemoveImage(int imageId)
        {
            var image = _images.FirstOrDefault(i => i.Id == new ImageId(imageId));
            if (image is null)
                return false;
            return _images.Remove(image);
        }

        public void AddTag(TagId tagId)
        {
            if (_itemTags.Any(it => it.TagId == tagId))
                return;
            _itemTags.Add(ItemTag.Create(Id, tagId));
        }

        public void RemoveTag(TagId tagId)
        {
            var itemTag = _itemTags.FirstOrDefault(it => it.TagId == tagId);
            if (itemTag is not null)
            {
                _itemTags.Remove(itemTag);
            }
        }

        public void ReplaceTags(IEnumerable<TagId> tagIds)
        {
            _itemTags.Clear();
            foreach (var tagId in tagIds)
            {
                _itemTags.Add(ItemTag.Create(Id, tagId));
            }
        }
    }
}
