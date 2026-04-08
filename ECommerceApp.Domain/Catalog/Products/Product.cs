using ECommerceApp.Domain.Catalog.Products.Events;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Product
    {
        public ProductId Id { get; private set; }
        public ProductName Name { get; private set; } = default!;
        public Price Cost { get; private set; } = default!;
        public ProductDescription Description { get; private set; } = default!;
        public ProductStatus Status { get; private set; }
        public CategoryId CategoryId { get; private set; } = default!;

        private readonly List<Image> _images = new();
        public IReadOnlyList<Image> Images => _images.Where(i => !i.IsDeleted).ToList().AsReadOnly();

        private readonly List<ProductTag> _productTags = new();
        public IReadOnlyList<ProductTag> ProductTags => _productTags.AsReadOnly();

        private Product() { }

        public static Product Create(
            string name,
            decimal cost,
            string description,
            int categoryId)
        {
            if (categoryId <= 0)
                throw new DomainException("CategoryId must be positive.");

            return new Product
            {
                Name = new ProductName(name),
                Cost = new Price(cost),
                Description = new ProductDescription(description),
                Status = ProductStatus.Draft,
                CategoryId = new CategoryId(categoryId)
            };
        }

        public ProductPublished Publish()
        {
            if (Status == ProductStatus.Published)
                throw new DomainException("Product is already published.");
            Status = ProductStatus.Published;
            return new ProductPublished(Id?.Value ?? 0, DateTime.UtcNow);
        }

        public ProductUnpublished Unpublish(UnpublishReason reason)
        {
            if (Status != ProductStatus.Published)
                throw new DomainException("Only published products can be unpublished.");
            Status = ProductStatus.Unpublished;
            return new ProductUnpublished(Id?.Value ?? 0, reason, DateTime.UtcNow);
        }

        public ProductDiscontinued Discontinue()
        {
            if (Status == ProductStatus.Discontinued)
                throw new DomainException("Product is already discontinued.");
            Status = ProductStatus.Discontinued;
            return new ProductDiscontinued(Id?.Value ?? 0, DateTime.UtcNow);
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

        public void AddImage(string fileName, string fileSource, string provider, int? imageId = null)
        {
            var activeImages = _images.Where(i => !i.IsDeleted).ToList();
            if (activeImages.Count >= 5)
            {
                throw new DomainException("A product can have at most 5 images.");
            }

            var sortOrder = activeImages.Count;
            var isMain = activeImages.Count == 0;
            _images.Add(Image.Create(Id, fileName, fileSource, provider, isMain, sortOrder, imageId.HasValue ? new ImageId(imageId.Value) : null));
        }

        public void SetMainImage(int imageId)
        {
            var target = _images.FirstOrDefault(i => i.Id == new ImageId(imageId) && !i.IsDeleted);
            if (target is null)
                throw new DomainException($"Image '{imageId}' not found on this product.");

            foreach (var img in _images.Where(i => !i.IsDeleted))
                img.ClearMain();

            target.SetAsMain();
        }

        public bool RemoveImage(int imageId)
        {
            var image = _images.FirstOrDefault(i => i.Id == new ImageId(imageId) && !i.IsDeleted);
            if (image is null)
                return false;

            var wasMain = image.IsMain;
            image.SoftDelete();

            var activeImages = _images.Where(i => !i.IsDeleted).ToList();
            if (wasMain && activeImages.Count > 0)
                activeImages[0].SetAsMain();

            for (var i = 0; i < activeImages.Count; i++)
                activeImages[i].Reorder(i);

            return true;
        }

        public void ReorderImages(IList<int> orderedImageIds)
        {
            if (orderedImageIds is null || orderedImageIds.Count == 0)
                throw new DomainException("Image order list cannot be empty.");

            var activeImages = _images.Where(i => !i.IsDeleted).ToList();
            if (orderedImageIds.Count != activeImages.Count)
                throw new DomainException("Image order list must contain all product image IDs.");

            for (var i = 0; i < orderedImageIds.Count; i++)
            {
                var image = activeImages.FirstOrDefault(img => img.Id == new ImageId(orderedImageIds[i]));
                if (image is null)
                    throw new DomainException($"Image '{orderedImageIds[i]}' not found on this product.");
                image.Reorder(i);
            }
        }

        public void AddTag(TagId tagId)
        {
            if (_productTags.Any(pt => pt.TagId == tagId))
                return;
            _productTags.Add(ProductTag.Create(Id, tagId));
        }

        public void RemoveTag(TagId tagId)
        {
            var productTag = _productTags.FirstOrDefault(pt => pt.TagId == tagId);
            if (productTag is not null)
                _productTags.Remove(productTag);
        }

        public void ReplaceTags(IEnumerable<TagId> tagIds)
        {
            _productTags.Clear();
            foreach (var tagId in tagIds)
                _productTags.Add(ProductTag.Create(Id, tagId));
        }
    }
}
