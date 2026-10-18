using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IManageEmailPage
    {
        Task<IManageEmailPage> ChangeEmailAsync(string newEmail);
        Task<string> GetStatusMessageAsync();
        Task<bool> HasFieldValidationErrorAsync();
    }
}
