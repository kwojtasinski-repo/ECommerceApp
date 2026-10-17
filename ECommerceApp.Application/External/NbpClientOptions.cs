namespace ECommerceApp.Application.External
{
    public sealed class NbpClientOptions
    {
        public const string SectionName = "NbpClient";

        public string BaseUrl { get; set; } = "http://api.nbp.pl";
    }
}
