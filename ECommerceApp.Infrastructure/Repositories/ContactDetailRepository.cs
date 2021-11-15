using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ContactDetailRepository : GenericRepository<ContactDetail>, IContactDetailRepository
    {
        public ContactDetailRepository(Context context) : base(context)
        {
        }

        public int AddContactDetail(ContactDetail contactDetail)
        {
            _context.ContactDetails.Add(contactDetail);
            _context.SaveChanges();
            return contactDetail.Id;
        }

        public void DeleteContactDetail(int contactDetailId)
        {
            var contactDetail = _context.ContactDetails.Find(contactDetailId);

            if (contactDetail != null)
            {
                _context.ContactDetails.Remove(contactDetail);
                _context.SaveChanges();
            }
        }

        public IQueryable<ContactDetail> GetAllContactDetails()
        {
            var contactDetails = _context.ContactDetails.AsQueryable();
            return contactDetails;
        }

        public ContactDetail GetContactDetailById(int contactDetailId)
        {
            var contactDetail = _context.ContactDetails
                           .Include(inc => inc.ContactDetailType)
                           .FirstOrDefault(c => c.Id == contactDetailId);
            return contactDetail;
        }

        public void UpdateContactDetail(ContactDetail contactDetail)
        {
            _context.Attach(contactDetail);
            _context.Entry(contactDetail).Property("ContactDetailInformation").IsModified = true;
            _context.Entry(contactDetail).Property("ContactDetailTypeId").IsModified = true;
            _context.SaveChanges();
        }

        public IQueryable<int> GetCustomersIds()
        {
            var customersIds = _context.Customers.AsQueryable().Select(c => c.Id);
            return customersIds;
        }

        public IQueryable<int> GetCustomersIds(Expression<Func<Customer, bool>> expression)
        {
            var customerIds = _context.Customers.AsQueryable().Where(expression).Select(c => c.Id);
            return customerIds;
        }

        public ContactDetail GetContactDetailById(int id, string userId)
        {
            var contactDetail = _context.ContactDetails.Include(c => c.Customer)
                                .Include(cdt => cdt.ContactDetailType)
                                    .Where(c => c.Id == id && c.Customer.UserId == userId).FirstOrDefault();

            return contactDetail;
        }
    }
}
