using System;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerRefundsVm : BaseVm
    {
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; } // 1:1 Only one Order can be refund
    }
}