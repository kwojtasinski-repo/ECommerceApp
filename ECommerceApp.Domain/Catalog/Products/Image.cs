using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Image
    {
        public ImageId Id { get; private set; }
        public ImageFileName FileName { get; private set; } = default!;
        public string FileSource { get; private set; } = default!;
        public string Provider { get; private set; } = default!;
        public bool IsMain { get; private set; }
        public int SortOrder { get; private set; }
        public bool IsDeleted { get; private set; }
        public ProductId ProductId { get; private set; } = default!;

        private Image() { }

        public static Image Create(
            ProductId productId,
            string fileName,
            string fileSource,
            string provider,
            bool isMain,
            int sortOrder,
            ImageId imageId = null)
        {
            if (sortOrder < 0)
            {
                throw new DomainException("SortOrder must not be negative.");
            }

            return new Image
            {
                Id = imageId,
                ProductId = productId,
                FileName = new ImageFileName(fileName),
                FileSource = fileSource ?? string.Empty,
                Provider = provider ?? string.Empty,
                IsMain = isMain,
                SortOrder = sortOrder,
                IsDeleted = false
            };
        }

        public void Reorder(int sortOrder)
        {
            if (sortOrder < 0)
            {
                throw new DomainException("SortOrder must not be negative.");
            }

            SortOrder = sortOrder;
        }

        internal void SetAsMain()
        {
            IsMain = true;
        }

        internal void ClearMain()
        {
            IsMain = false;
        }

        internal void SoftDelete()
        {
            IsDeleted = true;
        }
    }
}
