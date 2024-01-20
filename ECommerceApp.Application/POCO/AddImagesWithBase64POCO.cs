using System.Collections.Generic;

namespace ECommerceApp.Application.POCO
{
    public record AddImagesWithBase64POCO(int? ItemId, IEnumerable<FileWithBase64Format> FilesWithBase64Format);

    public record FileWithBase64Format(string Name, string FileSource);
}
