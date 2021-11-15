using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IContactDetailTypeRepository : IGenericRepository<ContactDetailType>
    {
        void DeleteContactDetailType(int brandId);
        int AddContactDetailType(ContactDetailType brand);
        ContactDetailType GetContactDetailTypeById(int brandId);
        IQueryable<ContactDetailType> GetAllContactDetailTypes();
        void UpdateContactDetailType(ContactDetailType brand);
    }
}
