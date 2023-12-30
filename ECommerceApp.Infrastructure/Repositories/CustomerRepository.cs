using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
    {
        public CustomerRepository(Context context) : base(context)
        {
        }

        public bool DeleteCustomer(int customerId)
        {
            if (!_context.Customers.Any(c => c.Id == customerId))
            {
                return false;
            }

            var customer = _context.Customers
                                    .Include(c => c.Addresses)
                                    .Include(c => c.ContactDetails)
                                    .Include(c => c.Payments)
                                    .Include(c => c.Refunds)
                                    .Include(c => c.Orders)
                                    .FirstOrDefault(p => p.Id == customerId);

            foreach (var address in customer.Addresses)
            {
                _context.Addresses.Remove(address);
            }

            foreach (var contactDetail in customer.ContactDetails)
            {
                _context.ContactDetails.Remove(contactDetail);
            }

            foreach (var payment in customer.Payments)
            {
                _context.Payments.Remove(payment);
            }

            foreach (var refund in customer.Refunds)
            {
                _context.Refunds.Remove(refund);
            }

            foreach (var order in customer.Orders)
            {
                _context.Orders.Remove(order);
            }

            _context.Customers.Remove(customer);
            _context.SaveChanges();
            return true;
        }

        public int AddCustomer(Customer customer)
        {
            _context.Customers.Add(customer);
            _context.SaveChanges();
            return customer.Id;
        }

        public Customer GetCustomerDetailsById(int id)
        {
            var customer = _context.Customers
                .Include(inc => inc.ContactDetails)
                .Include(inc => inc.ContactDetails).ThenInclude(inc => inc.ContactDetailType)
                .Include(inc => inc.Addresses)
                .Include(inc => inc.Orders).ThenInclude(inc => inc.OrderItems)
                .Include(inc => inc.Payments)
                .Include(inc => inc.Refunds)
                .FirstOrDefault(c => c.Id == id);
            return customer;
        }

        public Customer GetCustomerDetailsById(int id, string userId)
        {
            var customer = _context.Customers
                .Include(inc => inc.ContactDetails)
                .Include(inc => inc.ContactDetails).ThenInclude(inc => inc.ContactDetailType)
                .Include(inc => inc.Addresses)
                .Include(inc => inc.Orders).ThenInclude(inc => inc.OrderItems)
                .Include(inc => inc.Payments)
                .Include(inc => inc.Refunds)
                .FirstOrDefault(c => c.Id == id && c.UserId == userId);
            return customer;
        }

        public IQueryable<Customer> GetAllCustomers()
        {
            return _context.Customers;
        }

        public void UpdateCustomer(Customer customer)
        {
            _context.Attach(customer);
            _context.Entry(customer).Property("FirstName").IsModified = true;
            _context.Entry(customer).Property("LastName").IsModified = true;
            _context.Entry(customer).Property("IsCompany").IsModified = true;
            _context.Entry(customer).Property("NIP").IsModified = true;
            _context.Entry(customer).Property("CompanyName").IsModified = true;
            _context.Entry(customer).Collection("ContactDetails").IsModified = true;
            _context.Entry(customer).Collection("Addresses").IsModified = true;
            _context.SaveChanges();
        }

        public IQueryable<ContactDetailType> GetAllDetailTypes()
        {
            return _context.ContactDetailTypes;
        }

        public int AddContactDetailType(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Add(contactDetailType);
            _context.SaveChanges();
            return contactDetailType.Id;
        }

        public int AddNewContact(ContactDetail contactDetail)
        {
            _context.ContactDetails.Add(contactDetail);
            _context.SaveChanges();
            return contactDetail.Id;
        }

        public int AddNewAddress(Address address)
        {
            _context.Addresses.Add(address);
            _context.SaveChanges();
            return address.Id;
        }

        public Address GetAddressById(int id)
        {
            var address = _context.Addresses.FirstOrDefault(c => c.Id == id);
            return address;
        }

        public Address GetAddressById(int id, string userId)
        {
            var customers = _context.Customers.Where(c => c.UserId == userId).ToList();
            Address address = null;
            foreach (var cust in customers)
            {
                address = _context.Addresses.FirstOrDefault(c => c.Id == id && c.CustomerId == cust.Id);
                if (address != null)
                    break;
            }

            return address;
        }

        public ContactDetail GetContactDetailById(int id)
        {
            var contactDetail = _context.ContactDetails
                .Include(inc => inc.ContactDetailType)
                .FirstOrDefault(c => c.Id == id);
            return contactDetail;
        }

        public ContactDetail GetContactDetailById(int id, string userId)
        {
            var customers = _context.Customers.Where(c => c.UserId == userId).ToList();
            ContactDetail contactDetail = null;
            foreach (var cust in customers)
            {
                contactDetail = _context.ContactDetails
                                    .Include(inc => inc.ContactDetailType)
                                    .FirstOrDefault(c => c.Id == id && c.CustomerId == cust.Id);
            }

            return contactDetail;
        }

        public void UpdateAddress(Address address)
        {
            _context.Attach(address);
            _context.Entry(address).Property("Street").IsModified = true;
            _context.Entry(address).Property("BuildingNumber").IsModified = true;
            _context.Entry(address).Property("FlatNumber").IsModified = true;
            _context.Entry(address).Property("ZipCode").IsModified = true;
            _context.Entry(address).Property("City").IsModified = true;
            _context.Entry(address).Property("Country").IsModified = true;
            _context.SaveChanges();
        }

        public void UpdateContactDetail(ContactDetail contactDetail)
        {
            _context.Attach(contactDetail);
            _context.Entry(contactDetail).Property("ContactDetailInformation").IsModified = true;
            _context.Entry(contactDetail).Property("ContactDetailTypeId").IsModified = true;
            _context.SaveChanges();
        }

        public void DeleteAddress(int id)
        {
            var address = _context.Addresses.Find(id);

            if (address != null)
            {
                _context.Addresses.Remove(address);
                _context.SaveChanges();
            }
        }

        public void DeleteContactDetail(int id)
        {
            var contactDetail = _context.ContactDetails.Find(id);

            if (contactDetail != null)
            {
                _context.ContactDetails.Remove(contactDetail);
                _context.SaveChanges();
            }
        }

        public int AddNewContactDetailType(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Add(contactDetailType);
            _context.SaveChanges();
            return contactDetailType.Id;
        }

        public ContactDetailType GetContactDetailTypeById(int id)
        {
            var contactDetailType = _context.ContactDetailTypes.Find(id);
            return contactDetailType;
        }

        public void UpdateContactDetailType(ContactDetailType contactDetailType)
        {
            _context.Attach(contactDetailType);
            _context.Entry(contactDetailType).Property("Name").IsModified = true;
            _context.SaveChanges();
        }

        public bool CustomerExists(int id, string userId)
        {
            return _context.Customers.Where(c => c.Id == id && c.UserId == userId).AsNoTracking().Any();
        }

        public List<Customer> GetCustomersByUserId(string userId)
        {
            return _context.Customers.Where(c => c.UserId == userId).ToList();
        }

        public Customer GetCustomerById(int id)
        {
            var customer = _context.Customers
                .Include(inc => inc.ContactDetails)
                .Include(inc => inc.ContactDetails).ThenInclude(inc => inc.ContactDetailType)
                .Include(inc => inc.Addresses)
                .FirstOrDefault(c => c.Id == id);
            return customer;
        }

        public Customer GetCustomerById(int id, string userId)
        {
            var customer = _context.Customers
                .Include(inc => inc.ContactDetails)
                .Include(inc => inc.ContactDetails).ThenInclude(inc => inc.ContactDetailType)
                .Include(inc => inc.Addresses)
                .FirstOrDefault(c => c.Id == id && c.UserId == userId);
            return customer;
        }
    }
}
