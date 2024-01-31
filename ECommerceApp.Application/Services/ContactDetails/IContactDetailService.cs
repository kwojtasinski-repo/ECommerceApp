using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.ContactDetail;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public interface IContactDetailService
    {
        int AddContactDetail(ContactDetailDto contactDetailDto);
        bool DeleteContactDetail(int id);
        ContactDetailsForListVm GetContactDetail(int id);
        ContactDetailDto GetContactDetailById(int id);
        bool UpdateContactDetail(ContactDetailDto contactDetailDto);
        IEnumerable<ContactDetailDto> GetAllContactDetails();
        bool ContactDetailExists(int id);
    }
}
