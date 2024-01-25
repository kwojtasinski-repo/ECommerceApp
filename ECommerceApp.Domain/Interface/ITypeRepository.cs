using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface ITypeRepository
    {
        void DeleteType(int typeId);
        int AddType(Model.Type type);
        Model.Type GetTypeById(int typeId);
        IQueryable<Model.Type> GetAllTypes();
        void UpdateType(Model.Type type);
        bool ExistsById(int id);
    }
}
