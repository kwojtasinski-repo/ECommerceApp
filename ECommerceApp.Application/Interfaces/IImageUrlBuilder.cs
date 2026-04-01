namespace ECommerceApp.Application.Interfaces
{
    public interface IImageUrlBuilder
    {
        /// <summary>Returns the display URL (absolute when Images:BaseUrl is configured, root-relative otherwise).</summary>
        string Build(int imageId);

        /// <summary>Returns the canonical host-independent form used for cross-BC snapshot storage: <c>images/{id}</c>.</summary>
        string GetCanonical(int imageId);
    }
}
