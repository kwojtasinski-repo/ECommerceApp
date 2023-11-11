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
        ContactDetailVm GetContactDetailById(int id);
        void UpdateContactDetail(ContactDetailVm contactDetailVm);
        IEnumerable<ContactDetailVm> GetAllContactDetails(Expression<Func<ContactDetail, bool>> expression);
        bool ContactDetailExists(Expression<Func<ContactDetail, bool>> expression);
        bool ContactDetailExists(int id);
    }
}
