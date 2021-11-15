using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IContactDetailTypeService : IAbstractService<ContactDetailTypeVm, IContactDetailTypeRepository, ContactDetailType>
    {
        IEnumerable<ContactDetailTypeVm> GetContactDetailTypes(Expression<Func<ContactDetailType, bool>> expression);
    }
}
