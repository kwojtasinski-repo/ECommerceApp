using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Image
    {
        public ImageId Id { get; private set; } = new ImageId(0);
        public ImageFileName FileName { get; private set; } = default!;
        public bool IsMain { get; private set; }
        public int SortOrder { get; private set; }
        public ProductId ProductId { get; private set; } = default!;

        private Image() { }

        public static Image Create(ProductId productId, string fileName, bool isMain, int sortOrder)
        {
            if (productId is null)
                throw new DomainException("ProductId is required.");
            if (sortOrder < 0)
                throw new DomainException("SortOrder must not be negative.");

            return new Image
            {
                ProductId = productId,
                FileName = new ImageFileName(fileName),
                IsMain = isMain,
                SortOrder = sortOrder
            };
        }

        public void Reorder(int sortOrder)
        {
            if (sortOrder < 0)
                throw new DomainException("SortOrder must not be negative.");
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
    }
}
