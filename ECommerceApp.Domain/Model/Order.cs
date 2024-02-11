using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Model
{
    public class Order : BaseEntity
    {
        public string Number { get; set; }
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public virtual CouponUsed CouponUsed { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int? PaymentId { get; set; }
        public bool IsPaid { get; set; }
        public virtual Payment Payment { get; set; }
        public int? RefundId { get; set; }
        public virtual Refund Refund { get; set; }
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }

        public void CalculateCost()
        {
            if (OrderItems is null || !OrderItems.Any())
            {
                Cost = 0;
                return;
            }

            var cost = 0M;
            var discount = (1 - (CouponUsed?.Coupon?.Discount / 100M) ?? 1);
            foreach (var orderItem in OrderItems)
            {
                cost += orderItem.Item.Cost * orderItem.ItemOrderQuantity * discount;
            }
            Cost = cost;
        }

        public Order Copy()
        {
            return new Order
            {
                Id = Id,
                Cost = Cost,
                Number = Number,
                Ordered = Ordered,
                IsPaid = IsPaid,
                PaymentId = PaymentId,
                Delivered = Delivered,
                IsDelivered = IsDelivered,
                CustomerId = CustomerId,
                CurrencyId = CurrencyId,
                CouponUsedId = CouponUsedId,
                RefundId = RefundId,
                UserId = UserId,
                OrderItems = OrderItems?.Select(oi => new OrderItem
                {
                    Id = oi.Id,
                    ItemId = oi.ItemId,
                    ItemOrderQuantity = oi.ItemOrderQuantity,
                    OrderId = oi.OrderId,
                    CouponUsedId = oi.CouponUsedId,
                    UserId = oi.UserId,
                    RefundId = oi.RefundId
                }).ToList(),
                CouponUsed = CouponUsed is not null ? new CouponUsed
                {
                    Id = CouponUsed.Id,
                    CouponId = CouponUsed.CouponId,
                    OrderId = Id,
                    Coupon = CouponUsed.Coupon is not null ? new Coupon
                    {
                        Id = CouponUsed.Coupon.Id,
                        Code = CouponUsed.Coupon.Code,
                        Description = CouponUsed.Coupon.Description,
                        Discount = CouponUsed.Coupon.Discount,
                        CouponTypeId = CouponUsed.Coupon.CouponTypeId,
                        CouponUsedId = CouponUsedId,
                        Type = CouponUsed.Coupon.Type,
                    } : null,
                } : null,
                Currency = Currency is not null ? new Currency
                {
                    Id = Currency.Id,
                    Code = Currency.Code,
                    Description = Currency.Description,
                } : null,
                User = User is not null ? new ApplicationUser
                {
                    Id = User.Id,
                    AccessFailedCount = User.AccessFailedCount,
                    ConcurrencyStamp = User.ConcurrencyStamp,
                    Email = User.Email,
                    EmailConfirmed = User.EmailConfirmed,
                    LockoutEnabled = User.LockoutEnabled,
                    LockoutEnd = User.LockoutEnd,
                    NormalizedEmail = User.NormalizedEmail,
                    NormalizedUserName = User.NormalizedUserName,
                    UserName = User.UserName,
                } : null,
                Customer = Customer is not null ? new Customer
                {
                    Id = Customer.Id,
                    FirstName = Customer.FirstName,
                    LastName = Customer.LastName,
                    IsCompany = Customer.IsCompany,
                    CompanyName = Customer.CompanyName,
                    NIP = Customer.NIP,
                    UserId = Customer.UserId,
                    Addresses = Customer.Addresses?.Select(a => new Address
                    {
                        Id = a.Customer.Id,
                        BuildingNumber = a.BuildingNumber,
                        City = a.City,
                        Country = a.Country,
                        CustomerId = a.CustomerId,
                        FlatNumber = a.FlatNumber,
                        Street = a.Street,
                        ZipCode = a.ZipCode
                    }).ToList(),
                    ContactDetails = Customer.ContactDetails?.Select(cd => new ContactDetail
                    {
                        Id = cd.Id,
                        ContactDetailInformation = cd.ContactDetailInformation,
                        ContactDetailTypeId = cd.ContactDetailTypeId,
                        CustomerId = cd.CustomerId
                    }).ToList(),
                } : null,
                Payment = Payment is not null ? new Payment
                {
                    Id = Payment.Id,
                    Cost = Payment.Cost,
                    CurrencyId = Payment.CurrencyId,
                    CustomerId = Payment.CustomerId,
                    DateOfOrderPayment = Payment.DateOfOrderPayment,
                    Number = Payment.Number,
                    OrderId = Payment.OrderId,
                    State = Payment.State
                } : null,
                Refund = Refund is not null ? new Refund
                {
                    Id = Refund.Id,
                    Accepted = Refund.Accepted,
                    OnWarranty = Refund.OnWarranty,
                    Reason = Refund.Reason,
                    RefundDate = Refund.RefundDate,
                    OrderId = Refund.OrderId,
                    CustomerId = Refund.CustomerId
                } : null,
            };
        }
    }
}
