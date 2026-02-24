using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Image
    {
        public ImageId Id { get; private set; } = new ImageId(0);
        public string FileName { get; private set; } = default!;
        public bool IsMain { get; private set; }
        public int SortOrder { get; private set; }
        public ItemId ItemId { get; private set; } = default!;

        private Image() { }

        public static Image Create(ItemId itemId, string fileName, bool isMain, int sortOrder)
        {
            if (itemId is null)
                throw new DomainException("ItemId is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new DomainException("FileName is required.");
            if (sortOrder < 0)
                throw new DomainException("SortOrder must not be negative.");

            return new Image
            {
                ItemId = itemId,
                FileName = fileName.Trim(),
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

        public void SetAsMain()
        {
            IsMain = true;
        }

        public void UnsetMain()
        {
            IsMain = false;
        }
    }
}
