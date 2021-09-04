using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class ContactDetailVm : BaseVm
    {
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public int CustomerId { get; set; }

        public NewContactDetailVm MapToNewContactDetail()
        {
            var contact = new NewContactDetailVm()
            {
                Id = this.Id,
                ContactDetailInformation = this.ContactDetailInformation,
                ContactDetailTypeId = this.ContactDetailTypeId,
                CustomerId = this.CustomerId
            };

            return contact;
        }
    }

    public class ContactDetailValidation : AbstractValidator<ContactDetailVm>
    {
        public ContactDetailValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.ContactDetailTypeId).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
