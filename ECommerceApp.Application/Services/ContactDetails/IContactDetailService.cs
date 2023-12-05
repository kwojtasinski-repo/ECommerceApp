using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public interface IContactDetailService
    {
        int AddContactDetail(ContactDetailDto contactDetailDto);
        bool DeleteContactDetail(int id);
        ContactDetailsForListVm GetContactDetails(int id);
        ContactDetailDto GetContactDetailById(int id);
        bool UpdateContactDetail(ContactDetailDto contactDetailDto);
        IEnumerable<ContactDetailDto> GetAllContactDetails(Expression<Func<ContactDetail, bool>> expression);
        bool ContactDetailExists(Expression<Func<ContactDetail, bool>> expression);
        bool ContactDetailExists(int id);
    }
}
