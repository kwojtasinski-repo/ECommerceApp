using ECommerceApp.Application.DTO;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public interface IContactDetailTypeService
    {
        IEnumerable<ContactDetailTypeDto> GetContactDetailTypes();
        bool ContactDetailTypeExists(int id);
        int AddContactDetailType(ContactDetailTypeDto model);
        ContactDetailTypeDto GetContactDetailType(int id);
        void UpdateContactDetailType(ContactDetailTypeDto model);
    }
}
