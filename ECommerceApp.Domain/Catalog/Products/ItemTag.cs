namespace ECommerceApp.Domain.Catalog.Products
{
    public class ItemTag
    {
        public ItemId ItemId { get; private set; } = default!;
        public TagId TagId { get; private set; } = default!;

        private ItemTag() { }

        public static ItemTag Create(ItemId itemId, TagId tagId)
        {
            return new ItemTag
            {
                ItemId = itemId,
                TagId = tagId
            };
        }
    }
}
