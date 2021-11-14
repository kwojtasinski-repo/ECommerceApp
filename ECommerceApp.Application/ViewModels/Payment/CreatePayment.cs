using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class CreatePayment : BaseVm
    {
        public int OrderId { get; set; }

        public class CreatePaymentValidation : AbstractValidator<CreatePayment>
        {
            public CreatePaymentValidation()
            {
                RuleFor(x => x.Id).NotNull();
            }
        }
    }
}
