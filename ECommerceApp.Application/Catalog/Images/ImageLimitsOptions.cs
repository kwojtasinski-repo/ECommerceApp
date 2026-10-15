namespace ECommerceApp.Application.Catalog.Images
{
    /// <summary>
    /// Decoded-size limits enforced on uploaded images before they are decoded and re-encoded.
    /// Bound from the <c>Catalog:Images</c> configuration section; defaults apply when it is absent.
    /// The file size limit lives in <c>ImageConstraints</c>.
    /// </summary>
    public sealed class ImageLimitsOptions
    {
        public const string SectionName = "Catalog:Images";

        /// <summary>Maximum declared image width in pixels.</summary>
        public int MaxWidth { get; init; } = 10000;

        /// <summary>Maximum declared image height in pixels.</summary>
        public int MaxHeight { get; init; } = 10000;

        /// <summary>Maximum declared width * height. Bounds the memory of the decode (about 4 bytes per pixel).</summary>
        public long MaxPixels { get; init; } = 40_000_000;
    }
}
