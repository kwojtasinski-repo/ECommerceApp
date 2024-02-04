using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Type;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Items
{
    public interface ITypeService
    {
        int AddType(TypeDto model);
        TypeDetailsVm GetTypeDetails(int id);
        TypeDto GetTypeById(int id);
        bool UpdateType(TypeDto model);
        IEnumerable<TypeDto> GetTypes();
        ListForTypeVm GetTypes(int pageSize, int pageNo, string searchString);
        bool TypeExists(int id);
        bool DeleteType(int id);
    }
}
