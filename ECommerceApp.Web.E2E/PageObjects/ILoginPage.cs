using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface ILoginPage
    {
        Task LoginAsync(string email, string password);
        Task<ILoginPage> SubmitLogin(string email, string password);
        Task<bool> IsDisplayed();
        Task<bool> HasValidationError();
    }
}