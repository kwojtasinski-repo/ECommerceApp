using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IContactDetailTypeRepository : IGenericRepository<ContactDetailType>
    {
        void DeleteContactDetailType(int contactDetailTypeId);
        int AddContactDetailType(ContactDetailType contactDetailType);
        ContactDetailType GetContactDetailTypeById(int contactDetailType);
        IQueryable<ContactDetailType> GetAllContactDetailTypes();
        void UpdateContactDetailType(ContactDetailType contactDetailType);
    }
}
