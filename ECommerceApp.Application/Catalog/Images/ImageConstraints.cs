using System;
using System.IO;

namespace ECommerceApp.Application.Catalog.Images
{
    internal static class ImageConstraints
    {
        public const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        public const int MaxImagesPerItem = 5;
        public static readonly string[] AllowedExtensions = { ".jpg", ".png" };

        private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] PngEnd = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
        private const int TrailerWindow = 64;

        /// <summary>
        /// Checks that the content really is the image type promised by <paramref name="extension"/>:
        /// magic bytes at the start and the end-of-image marker at the end (rejects renamed non-images
        /// and truncated files). The stream must be seekable; its position is not preserved.
        /// </summary>
        public static bool HasValidImageContent(string extension, Stream content)
        {
            if (content is null || !content.CanSeek)
            {
                return false;
            }

            return extension switch
            {
                ".jpg" => StartsWith(content, JpegSignature) && JpegEndsWithEoi(content),
                ".png" => StartsWith(content, PngSignature) && EndsWith(content, PngEnd),
                _ => false
            };
        }

        private static bool StartsWith(Stream content, byte[] expected)
        {
            var buffer = new byte[expected.Length];
            content.Seek(0, SeekOrigin.Begin);
            return content.Length >= expected.Length
                && content.Read(buffer, 0, buffer.Length) == buffer.Length
                && buffer.AsSpan().SequenceEqual(expected);
        }

        private static bool EndsWith(Stream content, byte[] expected)
        {
            if (content.Length < expected.Length + PngSignature.Length)
            {
                return false;
            }

            var buffer = new byte[expected.Length];
            content.Seek(-expected.Length, SeekOrigin.End);
            return content.Read(buffer, 0, buffer.Length) == buffer.Length
                && buffer.AsSpan().SequenceEqual(expected);
        }

        private static bool JpegEndsWithEoi(Stream content)
        {
            var window = (int)Math.Min(TrailerWindow, content.Length);
            var buffer = new byte[window];
            content.Seek(-window, SeekOrigin.End);
            if (content.Read(buffer, 0, window) != window)
            {
                return false;
            }

            // Tolerate zero padding after the EOI marker (FF D9), nothing else.
            var end = window;
            while (end > 0 && buffer[end - 1] == 0x00)
            {
                end--;
            }

            return end >= 2 && buffer[end - 2] == 0xFF && buffer[end - 1] == 0xD9;
        }
    }
}
