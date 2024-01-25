using ECommerceApp.Domain.Model;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Domain.Interface
{
    public interface IContactDetailRepository
    {
        bool DeleteContactDetail(int contactDetailId);
        int AddContactDetail(ContactDetail contactDetail);
        ContactDetail GetContactDetailById(int contactDetailId);
        IQueryable<ContactDetail> GetAllContactDetails();
        void UpdateContactDetail(ContactDetail contactDetail);
        IQueryable<int> GetCustomersIds();
        IQueryable<int> GetCustomersIds(Expression<Func<Customer,bool>> expression);
        ContactDetail GetContactDetailById(int id, string userId);
    }
}
