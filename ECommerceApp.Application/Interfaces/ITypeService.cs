using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ITypeService : IAbstractService<TypeVm, ITypeRepository, Domain.Model.Type>
    {
        int AddType(TypeVm model);
        TypeDetailsVm GetTypeDetails(int id);
        TypeVm GetTypeById(int id);
        void UpdateType(TypeVm model);
        IEnumerable<TypeVm> GetTypes(Expression<Func<Domain.Model.Type, bool>> expression);
        ListForTypeVm GetTypes(int pageSize, int pageNo, string searchString);
        bool TypeExists(int id);
        void DeleteType(int id);
    }
}
