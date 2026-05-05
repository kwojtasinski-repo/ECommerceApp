using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.ContactDetail;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerDetailsVm
    {
        public CustomerDto Customer { get; set; }
        public virtual List<ContactDetailsForListVm> ContactDetails { get; set; } = new List<ContactDetailsForListVm>();
        public virtual List<AddressDto> Addresses { get; set; } = new List<AddressDto>();
        public virtual List<CustomerOrdersVm> Orders { get; set; } = new List<CustomerOrdersVm>();
        public List<CustomerPaymentsVm> Payments { get; set; } = new List<CustomerPaymentsVm>();
        public List<CustomerRefundsVm> Refunds { get; set; } = new List<CustomerRefundsVm>();
    }
}
