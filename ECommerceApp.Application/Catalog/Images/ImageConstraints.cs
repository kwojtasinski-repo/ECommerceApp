namespace ECommerceApp.Application.Catalog.Images
{
    internal static class ImageConstraints
    {
        public const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        public const int MaxImagesPerItem = 5;
        public static readonly string[] AllowedExtensions = { ".jpg", ".png" };
    }
}
