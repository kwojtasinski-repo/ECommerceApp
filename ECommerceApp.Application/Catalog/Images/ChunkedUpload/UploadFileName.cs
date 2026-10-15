using ECommerceApp.Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    /// <summary>
    /// Turns a client-supplied file name into a safe display name. The result is never used as a disk path
    /// by the upload service; it only names the image record.
    /// </summary>
    internal static class UploadFileName
    {
        private const int MaxLength = 100;
        private static readonly char[] ForbiddenChars = { ':', '*', '?', '"', '<', '>', '|' };
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string Normalize(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                throw Invalid("File name is required.");
            }

            // Only the last path segment is kept, whatever separator the client (or its OS) used.
            var name = rawName[(rawName.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];

            if (name.Length == 0 || name.Length > MaxLength)
            {
                throw Invalid("File name is empty or too long.");
            }

            if (name.Any(char.IsControl) || name.IndexOfAny(ForbiddenChars) >= 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                throw Invalid("File name contains invalid characters.");
            }

            if (name.EndsWith('.') || name.EndsWith(' ') || name.StartsWith(' '))
            {
                throw Invalid("File name must not start with a space or end with a dot or a space.");
            }

            var extension = System.IO.Path.GetExtension(name);
            var stem = name[..^extension.Length];
            if (stem.Length == 0 || stem.TrimEnd('.', ' ').Length == 0 || ReservedNames.Contains(stem.Split('.')[0]))
            {
                throw Invalid("File name is not allowed.");
            }

            var normalizedExtension = extension.ToLowerInvariant();
            if (!ImageConstraints.AllowedExtensions.Contains(normalizedExtension))
            {
                throw new BusinessException(
                    $"Extension '{extension}' is not allowed.",
                    ErrorCode.Create("fileExtensionNotAllowed", new List<ErrorParameter>
                    {
                        ErrorParameter.Create("name", name),
                        ErrorParameter.Create("extension", extension),
                        ErrorParameter.Create("extensions", string.Join(", ", ImageConstraints.AllowedExtensions))
                    }));
            }

            return stem + normalizedExtension;
        }

        private static BusinessException Invalid(string message)
            => new(message, ErrorCode.Create("invalidUploadFileName"));
    }
}
