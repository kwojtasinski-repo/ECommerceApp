using Microsoft.Extensions.Options;
using SkiaSharp;
using System.IO;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    internal sealed class ImageSanitizer : IImageSanitizer
    {
        private const int JpegQuality = 90;
        private readonly ImageLimitsOptions _limits;

        public ImageSanitizer(IOptions<ImageLimitsOptions> limits)
        {
            _limits = limits.Value;
        }

        public ImageSanitizeResult Sanitize(Stream content, string extension)
        {
            if (!ImageConstraints.HasValidImageContent(extension, content))
            {
                return ImageSanitizeResult.Rejected(ImageRejection.InvalidContent);
            }

            var expectedFormat = extension == ".png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
            content.Seek(0, SeekOrigin.Begin);

            // Creating the codec reads the header only; nothing is decoded yet.
            using var codec = SKCodec.Create(content, out var codecResult);
            if (codec is null || codecResult != SKCodecResult.Success || codec.EncodedFormat != expectedFormat)
            {
                return ImageSanitizeResult.Rejected(ImageRejection.InvalidContent);
            }

            var width = codec.Info.Width;
            var height = codec.Info.Height;
            if (width <= 0 || height <= 0)
            {
                return ImageSanitizeResult.Rejected(ImageRejection.InvalidContent, width, height);
            }

            if (width > _limits.MaxWidth || height > _limits.MaxHeight || (long)width * height > _limits.MaxPixels)
            {
                return ImageSanitizeResult.Rejected(ImageRejection.DimensionsTooLarge, width, height);
            }

            var alpha = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Unpremul;
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, alpha);
            using var decoded = new SKBitmap(info);
            if (codec.GetPixels(info, decoded.GetPixels()) != SKCodecResult.Success)
            {
                // Truncated or corrupted pixel data (IncompleteInput, InvalidInput, ...).
                return ImageSanitizeResult.Rejected(ImageRejection.InvalidContent, width, height);
            }

            using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin, out var orientedBitmap);
            var source = orientedBitmap ?? decoded;
            using var image = SKImage.FromBitmap(source);
            using var encoded = image.Encode(expectedFormat, expectedFormat == SKEncodedImageFormat.Jpeg ? JpegQuality : 100);
            if (encoded is null)
            {
                return ImageSanitizeResult.Rejected(ImageRejection.InvalidContent, width, height);
            }

            return ImageSanitizeResult.Accepted(encoded.ToArray(), source.Width, source.Height);
        }

        /// <summary>
        /// Bakes the EXIF orientation into the pixels (the metadata itself is dropped by the re-encode).
        /// Returns null in <paramref name="result"/> when the image is already upright.
        /// </summary>
        private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin, out SKBitmap result)
        {
            var w = source.Width;
            var h = source.Height;
            SKMatrix matrix;
            bool swap = false;
            switch (origin)
            {
                case SKEncodedOrigin.TopRight:
                    matrix = new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1);
                    break;
                case SKEncodedOrigin.BottomRight:
                    matrix = new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    matrix = new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1);
                    break;
                case SKEncodedOrigin.LeftTop:
                    matrix = new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1);
                    swap = true;
                    break;
                case SKEncodedOrigin.RightTop:
                    matrix = new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1);
                    swap = true;
                    break;
                case SKEncodedOrigin.RightBottom:
                    matrix = new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1);
                    swap = true;
                    break;
                case SKEncodedOrigin.LeftBottom:
                    matrix = new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1);
                    swap = true;
                    break;
                default:
                    result = null;
                    return null;
            }

            result = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, source.ColorType, source.AlphaType));
            using var canvas = new SKCanvas(result);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(source, 0, 0);
            return result;
        }
    }
}
