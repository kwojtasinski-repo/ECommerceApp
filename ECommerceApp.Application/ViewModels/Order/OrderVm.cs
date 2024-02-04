using ECommerceApp.Application.DTO;
using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderVm
    {
        public OrderDto Order { get; set; }
        public List<CustomerInformationForOrdersVm> Customers { get; set; } = new List<CustomerInformationForOrdersVm>();
        public CustomerDetailsDto NewCustomer { get; set; }
        public bool CustomerData { get; set; }
        public string PromoCode { get; set; }
    }

    public class OrderVmValidator : AbstractValidator<OrderVm>
    {
        public OrderVmValidator()
        {
            When(o => !o.CustomerData, () =>
            {
                RuleFor(c => c.NewCustomer).NotNull().SetValidator(new CustomerDetailsDtoValidator());
            });
        }
    }
}
