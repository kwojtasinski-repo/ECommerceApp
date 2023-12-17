using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Type;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Items
{
    public interface ITypeService
    {
        int AddType(TypeDto model);
        TypeDetailsVm GetTypeDetails(int id);
        TypeDto GetTypeById(int id);
        void UpdateType(TypeDto model);
        IEnumerable<TypeDto> GetTypes(Expression<Func<Domain.Model.Type, bool>> expression);
        ListForTypeVm GetTypes(int pageSize, int pageNo, string searchString);
        bool TypeExists(int id);
        void DeleteType(int id);
    }
}
