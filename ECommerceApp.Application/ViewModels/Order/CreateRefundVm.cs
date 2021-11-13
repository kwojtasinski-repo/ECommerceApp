using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class CreateRefundVm : BaseVm
    {
        public string Reason { get; set; }
        public int OrderId { get; set; }

        public RefundVm MapToNewRefund()
        {
            var refund = new RefundVm()
            {
                Id = this.Id,
                Reason = this.Reason,
                OrderId = this.OrderId
            };

            return refund;
        }
    }
}
