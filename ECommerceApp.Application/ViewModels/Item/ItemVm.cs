using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemVm : BaseVm, IMapFrom<Domain.Model.Item>
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public List<ItemTagVm> ItemTags { get; set; }

        public class ItemVmValidation : AbstractValidator<ItemVm>
        {
            public ItemVmValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.Name).NotNull();
                RuleFor(x => x.Cost).NotNull();
                RuleFor(x => x.Description).NotNull();
                RuleFor(x => x.Warranty).NotNull();
                RuleFor(x => x.Quantity).NotNull();
                RuleFor(x => x.BrandId).NotNull();
                RuleFor(x => x.TypeId).NotNull();

                When(x => x.ItemTags != null && x.ItemTags.Count > 0, () =>
                {
                    RuleForEach(it => it.ItemTags).SetValidator(new ItemTagVmValidator());
                });
            }
        }

        public class ItemTagVmValidator : AbstractValidator<ItemTagVm>
        {
            public ItemTagVmValidator()
            {
                RuleFor(it => it.TagId).GreaterThan(0);
            }
        }
    }
}
