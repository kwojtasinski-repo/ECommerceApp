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

        /// <summary>
        /// Selects the resumable-upload engine for the Catalog image upload widget.
        /// Values: "Classic" (default, custom chunked service) | "TUS" (tusdotnet middleware).
        /// </summary>
        public string ChunkedUploadImplementation { get; set; } = "Classic";

        /// <summary>Returns true when TUS protocol is selected as the upload engine.</summary>
        public bool UseTusUpload =>
            ChunkedUploadImplementation.Equals("TUS", System.StringComparison.OrdinalIgnoreCase);
    }
}
