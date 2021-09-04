using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemVm : BaseVm
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }

        public List<ItemTagVm> ItemTags { get; set; }

        public NewItemVm MapToNewItemVm()
        {
            var item = new NewItemVm()
            {
                Id = this.Id,
                Name = this.Name,
                Cost = this.Cost,
                Description = this.Description,
                Warranty = this.Warranty,
                Quantity = this.Quantity,
                BrandId = this.BrandId,
                TypeId = this.TypeId,
                ItemTags = new List<ItemsWithTagsVm>()
            };

            if (ItemTags != null && ItemTags.Count > 0)
            {
                var itemTags = new List<ItemsWithTagsVm>();
                foreach (var tag in ItemTags)
                {
                    var itemTag = new ItemsWithTagsVm
                    {
                        ItemId = item.Id,
                        TagId = tag.TagId
                    };

                    itemTags.Add(itemTag);
                }

                item.ItemTags = itemTags;
            }

            return item;
        }

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
