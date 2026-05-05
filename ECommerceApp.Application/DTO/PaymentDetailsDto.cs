using System;

namespace ECommerceApp.Application.DTO
{
    public class PaymentDetailsDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal Cost { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }
    }
}
