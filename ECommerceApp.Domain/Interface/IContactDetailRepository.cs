using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IContactDetailRepository
    {
        bool DeleteContactDetail(int contactDetailId);
        int AddContactDetail(ContactDetail contactDetail);
        ContactDetail GetContactDetailById(int contactDetailId);
        ContactDetail GetContactDetailByIdAndUserId(int contactDetailId, string userId);
        List<ContactDetail> GetAllContactDetails();
        void UpdateContactDetail(ContactDetail contactDetail);
        List<int> GetCustomersIds(string userId);
        ContactDetail GetContactDetailById(int id, string userId);
        bool ExistsByIdAndUserId(int id, string userId);
    }
}
