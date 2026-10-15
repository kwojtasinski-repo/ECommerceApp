using System.IO;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    internal interface IImageSanitizer
    {
        /// <summary>
        /// Verifies that <paramref name="content"/> really is the image promised by <paramref name="extension"/>,
        /// within the configured limits, and re-encodes it. The returned bytes carry pixels only: no metadata,
        /// no appended data. The stream must be seekable.
        /// </summary>
        ImageSanitizeResult Sanitize(Stream content, string extension);
    }

    internal enum ImageRejection
    {
        None,
        InvalidContent,
        DimensionsTooLarge
    }

    internal sealed record ImageSanitizeResult(byte[] Bytes, ImageRejection Rejection, int Width, int Height)
    {
        public bool IsAccepted => Rejection == ImageRejection.None;

        public static ImageSanitizeResult Accepted(byte[] bytes, int width, int height)
            => new(bytes, ImageRejection.None, width, height);

        public static ImageSanitizeResult Rejected(ImageRejection rejection, int width = 0, int height = 0)
            => new(null, rejection, width, height);
    }
}
