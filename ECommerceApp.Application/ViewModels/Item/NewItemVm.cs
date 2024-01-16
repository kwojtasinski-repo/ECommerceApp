using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Type;
using FluentValidation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class NewItemVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Item>
    {
        public NewItemVm()
        {
            ItemTags = new List<ItemsWithTagsVm>();
        }

        public string Name { get; set; }
        [DataType(DataType.Currency)]
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public List<BrandDto> Brands { get; set; }
        public List<TypeVm> Types { get; set; }
        public List<ItemsWithTagsVm> ItemTags { get; set; }
        public List<Image.GetImageVm> Images { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewItemVm, ECommerceApp.Domain.Model.Item>().ReverseMap()
                .ForMember(p => p.ItemTags, opt => opt.MapFrom(ps => ps.ItemTags))
                .ForMember(p => p.Images, opt => opt.Ignore());
        }
    }

    public class NewItemValidation : AbstractValidator<NewItemVm>
    {
        public NewItemValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull().MinimumLength(2).MaximumLength(100);
            RuleFor(x => x.Cost).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Description).NotNull().MinimumLength(2).MaximumLength(255);
            RuleFor(x => x.Warranty).NotNull();
            RuleFor(x => x.Quantity).NotNull().GreaterThan(0);
            RuleFor(x => x.BrandId).NotNull();
            RuleFor(x => x.TypeId).NotNull();
        }
    }
}
