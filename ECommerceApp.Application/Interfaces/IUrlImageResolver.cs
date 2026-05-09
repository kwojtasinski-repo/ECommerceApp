using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IUrlImageResolver
    {
        Task<ImageFileResult> ResolveAsync(int imageId);
    }

    public sealed record ImageFileResult(byte[] Bytes, string ContentType, string FileName);
}
