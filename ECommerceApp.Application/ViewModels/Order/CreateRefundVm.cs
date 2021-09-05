using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class CreateRefundVm : BaseVm
    {
        public string Reason { get; set; }
        public int OrderId { get; set; }

        public NewRefundVm MapToNewRefund()
        {
            var refund = new NewRefundVm()
            {
                Id = this.Id,
                Reason = this.Reason,
                OrderId = this.OrderId
            };

            return refund;
        }
    }
}
