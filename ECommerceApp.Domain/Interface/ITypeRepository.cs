using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ITypeRepository
    {
        void DeleteType(int typeId);
        int AddType(Model.Type type);
        Model.Type GetTypeById(int typeId);
        Model.Type GetTypeDetailsById(int typeId);
        List<Model.Type> GetAllTypes();
        List<Model.Type> GetAllTypes(int pageSize, int pageNo, string searchString);
        void UpdateType(Model.Type type);
        bool ExistsById(int id);
        int GetCountBySearchString(string searchString);
    }
}
