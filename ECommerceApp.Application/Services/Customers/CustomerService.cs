using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Customers
{
    public class CustomerService : ICustomerService
    {
        private readonly IMapper _mapper;
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository custRepo, IMapper mapper)
        {
            _mapper = mapper;
            _customerRepository = custRepo;
        }

        public int AddCustomer(CustomerDto newCustomer)
        {
            if (newCustomer is null)
            {
                throw new BusinessException($"{typeof(CustomerDto).Name} cannot be null");
            }

            if (newCustomer.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customer = _mapper.Map<Customer>(newCustomer);
            var id = _customerRepository.Add(customer);
            return id;
        }

        public int AddCustomerDetails(CustomerDetailsDto newCustomer)
        {
            if (newCustomer is null)
            {
                throw new BusinessException($"{typeof(CustomerDetailsDto).Name} cannot be null");
            }

            if (newCustomer.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customer = _mapper.Map<Customer>(newCustomer);
            var id = _customerRepository.Add(customer);
            return id;
        }

        public bool DeleteCustomer(int id)
        {
            return _customerRepository.DeleteCustomer(id);
        }

        public ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString)
        {
            var customers = _customerRepository.GetAllCustomers()
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
            var customers = _customerRepository.GetAllCustomers().Where(c => c.UserId == userId)
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

        public List<CustomerDetailsDto> GetAllCustomersForList()
        {
            var customers = _customerRepository.GetAllCustomers()
                .ProjectTo<CustomerDetailsDto>(_mapper.ConfigurationProvider)
                .ToList();

            return customers;
        }

        public CustomerDetailsVm GetCustomerDetails(int customerId)
        {
            var customer = _customerRepository.GetCustomerById(customerId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }

        public CustomerInformationForOrdersVm GetCustomerInformationById(int customerId)
        {
            var customer = _customerRepository.GetById(customerId);
            var customerVm = _mapper.Map<CustomerInformationForOrdersVm>(customer);

            return customerVm;
        }

        public CustomerDetailsVm GetCustomerDetails(int id, string userId)
        {
            var customer = _customerRepository.GetCustomerById(id, userId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }

        public CustomerDetailsDto GetCustomerForEdit(int id)
        {
            var customer = _customerRepository.GetCustomerById(id);
            var customerVm = _mapper.Map<CustomerDetailsDto>(customer);

            return customerVm;
        }

        public void UpdateCustomer(CustomerDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(CustomerDto).Name} cannot be null");
            }

            var customer = _customerRepository.GetById(model.Id)
                ?? throw new BusinessException($"Customer with id '{model.Id}' was not found");
            customer.FirstName = model.FirstName;
            customer.LastName = model.LastName;
            customer.IsCompany = model.IsCompany;
            customer.CompanyName = model.CompanyName;
            customer.NIP = model.NIP;
            _customerRepository.Update(customer);
        }

        public bool CustomerExists(int id, string userId)
        {
            var exists = _customerRepository.CustomerExists(id, userId);
            return exists;
        }

        public IEnumerable<CustomerDto> GetAllCustomers(Expression<Func<Customer, bool>> expression)
        {
            var customers = _customerRepository.GetAllCustomers().Where(expression).ToList();
            var customersVm = _mapper.Map<List<CustomerDto>>(customers);
            return customersVm;
        }

        public IQueryable<CustomerInformationForOrdersVm> GetCustomersInformationByUserId(string userId)
        {
            var customers = _customerRepository.GetAll().Where(c => c.UserId == userId)
                            .ProjectTo<CustomerInformationForOrdersVm>(_mapper.ConfigurationProvider);
            return customers;
        }

        #region AnnonymousUser TODO In Future

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

        #endregion
    }
}
