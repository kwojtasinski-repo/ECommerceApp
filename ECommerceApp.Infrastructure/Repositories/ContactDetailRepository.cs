using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ContactDetailRepository : IContactDetailRepository
    {
        private readonly Context _context;

        public ContactDetailRepository(Context context)
        {
            _context = context;
        }

        public int AddContactDetail(ContactDetail contactDetail)
        {
            _context.ContactDetails.Add(contactDetail);
            _context.SaveChanges();
            return contactDetail.Id;
        }

        public bool DeleteContactDetail(int contactDetailId)
        {
            var contactDetail = _context.ContactDetails.Find(contactDetailId);
            if (contactDetail is null)
            {
                return false;
            }

            _context.ContactDetails.Remove(contactDetail);
            return _context.SaveChanges() > 0;
        }

        public List<ContactDetail> GetAllContactDetails()
        {
            return _context.ContactDetails.ToList();
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

        public List<int> GetCustomersIds(string userId)
        {
            return _context.Customers
                        .Where(cd => cd.UserId == userId)
                        .Select(c => c.Id)
                        .ToList();
        }

        public ContactDetail GetContactDetailById(int id, string userId)
        {
            var contactDetail = _context.ContactDetails.Include(c => c.Customer)
                                .Include(cdt => cdt.ContactDetailType)
                                    .Where(c => c.Id == id && c.Customer.UserId == userId).FirstOrDefault();

            return contactDetail;
        }

        public ContactDetail GetContactDetailByIdAndUserId(int contactDetailId, string userId)
        {
            return _context.ContactDetails.FirstOrDefault(cd => cd.Id == contactDetailId && cd.Customer.UserId == userId);
        }

        public bool ExistsByIdAndUserId(int id, string userId)
        {
            return _context.ContactDetails
                .AsNoTracking()
                .Any(cd => cd.Id == id && cd.Customer.UserId == userId);
        }
    }
}
