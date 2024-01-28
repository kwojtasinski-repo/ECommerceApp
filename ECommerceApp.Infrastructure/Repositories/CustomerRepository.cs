using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly Context _context;

        public CustomerRepository(Context context)
        {
            _context = context;
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

        public Customer GetById(int customerId)
        {
            return _context.Customers
                .FirstOrDefault(c => c.Id == customerId);
        }

        public List<Customer> GetAllUserCustomers(string userId, int pageSize, int pageNo, string searchString)
        {
            return _context.Customers
                           .Where(c => c.UserId == userId)
                           .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();

        }

        public int GetCountBySearchStringAndUserId(string searchString, string userId)
        {
            return _context.Customers
                           .Where(c => c.UserId == userId)
                           .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                           .Count();

        }

        public List<Customer> GetAllCustomers(int pageSize, int pageNo, string searchString)
        {
            return _context.Customers
                           .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Customers
                           .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                           .Count();

        }

        public List<Customer> GetAllUserCustomers(string userId)
        {
            return _context.Customers
                           .Where(c => c.UserId == userId)
                           .ToList();
        }

        public bool ExistsById(int customerId)
        {
            return _context.Customers
                           .AsNoTracking()
                           .Any(c => c.Id == customerId);
        }
    }
}
