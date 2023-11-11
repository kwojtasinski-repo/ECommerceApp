using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services.Customers
{
    public class CustomerService : AbstractService<CustomerVm, ICustomerRepository, Customer>, ICustomerService
    {
        public CustomerService(ICustomerRepository custRepo, IMapper mapper) : base(custRepo, mapper)
        {
        }

        public int AddCustomer(NewCustomerVm newCustomer)
        {
            if (newCustomer is null)
            {
                throw new BusinessException($"{typeof(NewCustomerVm).Name} cannot be null");
            }

            if (newCustomer.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customer = _mapper.Map<Customer>(newCustomer);
            var id = _repo.Add(customer);
            return id;
        }

        public void DeleteCustomer(int id)
        {
            Delete(id);
        }

        public ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString)
        {
            var customers = _repo.GetAllCustomers()
                .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString));

            var customersToShow = customers.Skip(pageSize * (pageNo - 1)).Take(pageSize)
                .ProjectTo<CustomerForListVm>(_mapper.ConfigurationProvider)
                .ToList();

            var customersList = new ListForCustomerVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Customers = customersToShow,
                Count = customers.Count()
            };

            return customersList;
        }

        public ListForCustomerVm GetAllCustomersForList(string userId, int pageSize, int pageNo, string searchString)
        {
            var customers = _repo.GetAllCustomers().Where(c => c.UserId == userId)
                    .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                    || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                    .ProjectTo<CustomerForListVm>(_mapper.ConfigurationProvider);

            var customersToShow = customers.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var customersList = new ListForCustomerVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Customers = customersToShow,
                Count = customers.Count()
            };

            return customersList;
        }

        public List<NewCustomerVm> GetAllCustomersForList()
        {
            var customers = _repo.GetAllCustomers()
                .ProjectTo<NewCustomerVm>(_mapper.ConfigurationProvider)
                .ToList();

            return customers;
        }

        public CustomerDetailsVm GetCustomerDetails(int customerId)
        {
            var customer = _repo.GetCustomerById(customerId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }

        public CustomerInformationForOrdersVm GetCustomerInformationById(int customerId)
        {
            var customer = _repo.GetById(customerId);
            var customerVm = _mapper.Map<CustomerInformationForOrdersVm>(customer);

            return customerVm;
        }

        public CustomerDetailsVm GetCustomerDetails(int id, string userId)
        {
            var customer = _repo.GetCustomerById(id, userId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }

        public NewCustomerVm GetCustomerForEdit(int id)
        {
            var customer = _repo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerVm>(customer);

            return customerVm;
        }

        public void UpdateCustomer(NewCustomerVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewCustomerVm).Name} cannot be null");
            }

            var customer = _mapper.Map<Customer>(model);
            _repo.UpdateCustomer(customer);
        }

        private static string CreateRandomUser(int length = 10)
        {
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();

            // Select one random character at a time from the string  
            // and create an array of chars  
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }
            return new string(chars);
        }

        private static string CreateRandomPassword(int length = 15)
        {
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?_-";
            Random random = new Random();

            // Select one random character at a time from the string  
            // and create an array of chars  
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }
            return new string(chars);
        }

        public bool CustomerExists(int id, string userId)
        {
            var exists = _repo.CustomerExists(id, userId);
            return exists;
        }

        public IEnumerable<CustomerVm> GetAllCustomers(Expression<Func<Customer, bool>> expression)
        {
            var customers = _repo.GetAllCustomers().Where(expression).ToList();
            var customersVm = _mapper.Map<List<CustomerVm>>(customers);
            return customersVm;
        }

        public IQueryable<CustomerInformationForOrdersVm> GetCustomersInformationByUserId(string userId)
        {
            var customers = _repo.GetAll().Where(c => c.UserId == userId)
                            .ProjectTo<CustomerInformationForOrdersVm>(_mapper.ConfigurationProvider);
            return customers;
        }
    }
}
