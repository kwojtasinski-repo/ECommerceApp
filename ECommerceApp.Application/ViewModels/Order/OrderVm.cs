using ECommerceApp.Application.DTO;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderVm
    {
        public OrderDto Order { get; set; }
        public List<CustomerInformationForOrdersVm> Customers { get; set; }
        public CustomerDetailsDto NewCustomer { get; internal set; }
        public bool CustomerData { get; set; }
        public string PromoCode { get; set; }
    }
}
