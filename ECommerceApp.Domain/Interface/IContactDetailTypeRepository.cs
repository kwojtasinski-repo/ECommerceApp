using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IContactDetailTypeRepository
    {
        void DeleteContactDetailType(int contactDetailTypeId);
        int AddContactDetailType(ContactDetailType contactDetailType);
        ContactDetailType GetContactDetailTypeById(int contactDetailType);
        IQueryable<ContactDetailType> GetAllContactDetailTypes();
        void UpdateContactDetailType(ContactDetailType contactDetailType);
    }
}
