using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IContactDetailTypeRepository
    {
        void DeleteContactDetailType(int contactDetailTypeId);
        int AddContactDetailType(ContactDetailType contactDetailType);
        ContactDetailType GetContactDetailTypeById(int contactDetailType);
        List<ContactDetailType> GetAllContactDetailTypes();
        void UpdateContactDetailType(ContactDetailType contactDetailType);
    }
}
