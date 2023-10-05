using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ContactDetailTypeRepository : GenericRepository<ContactDetailType>, IContactDetailTypeRepository
    {
        public ContactDetailTypeRepository(Context context) : base(context)
        {
        }

        public int AddContactDetailType(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Add(contactDetailType);
            _context.SaveChanges();
            return contactDetailType.Id;
        }

        public void DeleteContactDetailType(int contactDetailTypeId)
        {
            var contactDetailType = GetById(contactDetailTypeId);

            if (contactDetailType != null)
            {
                _context.ContactDetailTypes.Remove(contactDetailType);
                _context.SaveChanges();
            }
        }

        public IQueryable<ContactDetailType> GetAllContactDetailTypes()
        {
            var contactDetailTypes = _context.ContactDetailTypes.AsQueryable();
            return contactDetailTypes;
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
