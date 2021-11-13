using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ITypeRepository : IGenericRepository<Model.Type>
    {
        void DeleteType(int typeId);
        int AddType(Model.Type type);
        Model.Type GetTypeById(int typeId);
        IQueryable<Model.Type> GetAllTypes();
        void UpdateType(Model.Type type);
    }
}
