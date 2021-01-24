using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public abstract class CustomerServiceAbstract : IBaseService<NewCustomerVm>
    {
        private readonly ICustomerRepository _custRepo;
        private readonly IMapper _mapper;

        public CustomerServiceAbstract(ICustomerRepository custRepo, IMapper mapper)
        {
            _custRepo = custRepo;
            _mapper = mapper;
        }

        public int Add(NewCustomerVm objectVm)
        {
            var customer = _mapper.Map<Customer>(objectVm);
            var id = _custRepo.AddCustomer(customer);
            return id;
        }

        public void Delete(int id)
        {
            _custRepo.DeleteCustomer(id);
        }

        public NewCustomerVm Get(int id)
        {
            var customer = _custRepo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerVm>(customer);

            return customerVm;
        }

        public List<NewCustomerVm> GetAll()
        {
            var customers = _custRepo.GetAllCustomers()
                .ProjectTo<NewCustomerVm>(_mapper.ConfigurationProvider)
                .ToList();

            return customers;
        }

        public List<NewCustomerVm> GetAll(string searchName)
        {
            var customers = _custRepo.GetAllCustomers()
                .Where(p => p.FirstName.StartsWith(searchName) || p.LastName.StartsWith(searchName)
                || p.CompanyName.StartsWith(searchName) || p.NIP.StartsWith(searchName))
                .ProjectTo<NewCustomerVm>(_mapper.ConfigurationProvider)
                .ToList();
            return customers;
        }

        public void Update(NewCustomerVm objectVm)
        {
            var customer = _mapper.Map<Customer>(objectVm);
            _custRepo.UpdateCustomer(customer);
        }

        public abstract int AddCustomer(NewCustomerVm newCustomer);
        public abstract void DeleteCustomer(int id);
        public abstract ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString);
        public abstract List<NewCustomerVm> GetAllCustomersForList();
        public abstract CustomerDetailsVm GetCustomerDetails(int customerId);
        public abstract CustomerDetailsVm GetCustomerDetails(int id, string userId);
        public abstract NewCustomerVm GetCustomerForEdit(int id);
        public abstract void UpdateCustomer(NewCustomerVm model);
        public abstract int CreateNewDetailContact(NewContactDetailVm newContact);
        public abstract int CreateNewAddress(NewAddressVm newAddress);
        public abstract NewAddressVm GetAddressForEdit(int id);
        public abstract NewContactDetailVm GetContactDetail(int id);
        public abstract NewContactDetailVm GetContactDetail(int id, string userId);
        public abstract void UpdateAddress(NewAddressVm model);
        public abstract void UpdateContactDetail(NewContactDetailVm model);
        public abstract void UpdateContactDetailType(NewContactDetailTypeVm model);
        public abstract void DeleteAddress(int id);
        public abstract void DeleteContactDetail(int id);
        public abstract AddressDetailVm GetAddressDetail(int id);
        public abstract AddressDetailVm GetAddressDetail(int id, string userId);
        public abstract bool CheckIfAddressExists(int id, string userId);
        public abstract IQueryable<ContactDetailTypeVm> GetConactDetailTypes();
        public abstract bool CheckIfCustomerExists(int id, string userId);
        public abstract bool CheckIfContactDetailExists(int id, string userId);
        public abstract bool CheckIfContactDetailType(int id);
        public abstract int AddAddress(NewAddressVm newAddress);
        public abstract int AddAddress(NewAddressVm model, string userId);
        public abstract int AddContactDetail(NewContactDetailVm newContactDetail);
        public abstract int AddContactDetail(NewContactDetailVm model, string userId);
        public abstract int AddContactDetailType(NewContactDetailTypeVm newContactDetailType);
        public abstract NewContactDetailTypeVm GetContactDetailType(int id);
    }
}
