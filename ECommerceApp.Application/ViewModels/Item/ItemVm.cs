using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System.Collections.Generic;

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

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ItemVm, ECommerceApp.Domain.Model.Item>().ReverseMap()
                .ForMember(t => t.Id, map => map.MapFrom(src => src.Id))
                .ForMember(t => t.Name, map => map.MapFrom(src => src.Name))
                .ForMember(t => t.Cost, map => map.MapFrom(src => src.Cost))
                .ForMember(t => t.Description, map => map.MapFrom(src => src.Description))
                .ForMember(t => t.Warranty, map => map.MapFrom(src => src.Warranty))
                .ForMember(t => t.BrandId, map => map.MapFrom(src => src.BrandId))
                .ForMember(t => t.TypeId, map => map.MapFrom(src => src.TypeId))
                .ForMember(t => t.CurrencyId, map => map.MapFrom(src => src.CurrencyId))
                .ForMember(t => t.ItemTags, map => map.MapFrom(src => src.ItemTags));
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
                RuleFor(x => x.CurrencyId).NotNull();

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
