using ECommerceApp.Application.DTO;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public interface IContactDetailTypeService
    {
        IEnumerable<ContactDetailTypeDto> GetContactDetailTypes(Expression<Func<ContactDetailType, bool>> expression);
        bool ContactDetailTypeExists(int id);
        int AddContactDetailType(ContactDetailTypeDto model);
        ContactDetailTypeDto GetContactDetailType(int id);
        void UpdateContactDetailType(ContactDetailTypeDto model);
    }
}
