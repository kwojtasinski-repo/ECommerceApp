using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface ICurrencyListPage
    {
        Task<ICurrencyListPage> SearchAsync(string searchString);
        Task<bool> ContainsCurrencyAsync(string code);
        Task<ICurrencyListPage> DeleteCurrencyAsync(string code);
    }
}
