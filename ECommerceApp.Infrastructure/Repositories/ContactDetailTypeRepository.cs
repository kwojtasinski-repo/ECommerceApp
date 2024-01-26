using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ContactDetailTypeRepository : IContactDetailTypeRepository
    {
        private readonly Context _context;

        public ContactDetailTypeRepository(Context context)
        {
            _context = context;
        }

        public int AddContactDetailType(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Add(contactDetailType);
            _context.SaveChanges();
            return contactDetailType.Id;
        }

        public void DeleteContactDetailType(int contactDetailTypeId)
        {
            var contactDetailType = GetContactDetailTypeById(contactDetailTypeId);

            if (contactDetailType != null)
            {
                _context.ContactDetailTypes.Remove(contactDetailType);
                _context.SaveChanges();
            }
        }

        public List<ContactDetailType> GetAllContactDetailTypes()
        {
            return _context.ContactDetailTypes.ToList();
        }

        public ContactDetailType GetContactDetailTypeById(int contactDetailTypeId)
        {
            var contactDetailType = _context.ContactDetailTypes.Find(contactDetailTypeId);
            return contactDetailType;
        }

        public void UpdateContactDetailType(ContactDetailType contactDetailType)
        {
            _context.Attach(contactDetailType);
            _context.Entry(contactDetailType).Property("Name").IsModified = true;
            _context.SaveChanges();
        }
    }
}
