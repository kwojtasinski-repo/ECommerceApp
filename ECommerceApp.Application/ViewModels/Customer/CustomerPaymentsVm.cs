using System;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerPaymentsVm : BaseVm
    {
        public string Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
    }
}
