using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IUserManagementPage
    {
        Task<bool> ContainsUserAsync(string userName);
        Task<IUserManagementPage> DeleteUserAsync(string userName);
    }
}
