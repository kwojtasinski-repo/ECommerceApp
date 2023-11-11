using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public interface IContactDetailService : IAbstractService<ContactDetailVm, IContactDetailRepository, ContactDetail>
    {
        int AddContactDetail(ContactDetailVm contactDetailVm);
        void DeleteContactDetail(int id);
        ContactDetailsForListVm GetContactDetails(int id);
        ContactDetailsForListVm GetContactDetails(int id, string userId);
        ContactDetailVm GetContactDetailById(int id);
        ContactDetailVm GetContactDetailById(int id, string userId);
        void UpdateContactDetail(ContactDetailVm contactDetailVm);
        IEnumerable<ContactDetailVm> GetAllContactDetails(Expression<Func<ContactDetail, bool>> expression);
        int AddContactDetail(ContactDetailVm model, string userId);
        bool ContactDetailExists(Expression<Func<ContactDetail, bool>> expression);
        bool ContactDetailExists(int id, string userId);
    }
}
