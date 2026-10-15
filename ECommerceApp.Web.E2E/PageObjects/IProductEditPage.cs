using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IProductEditPage
    {
        Task ChooseFileAsync(string filePath);
        Task ClickUploadAsync();
        Task<string> WaitForUploadErrorAsync();
        Task<string> WaitForResumeNoticeAsync();
        Task WaitForPageToSettleAsync();
        Task<int> GalleryImageCountAsync();
        Task WaitForGalleryImageCountAsync(int expected);
    }
}
