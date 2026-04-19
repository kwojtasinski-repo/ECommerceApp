namespace ECommerceApp.Web.Areas.Catalog.Options
{
    public sealed class CatalogOptions
    {
        public const string SectionName = "Catalog";

        /// <summary>
        /// When true, Create and Edit product views use the chunked upload widget
        /// (AddItemNew / EditItemNew). When false, the classic single-POST upload is used.
        /// </summary>
        public bool UseChunkedUpload { get; set; } = false;
    }
}
