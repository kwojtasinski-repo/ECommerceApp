using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    public interface IDatabaseInitializer
    {
        Task Initialize();
    }
}
