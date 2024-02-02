using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using FluentValidation;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.ViewModels.Tag
{
    public class TagDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Tag>
    {
        public string Name { get; set; }

        public List<ItemVm> ItemTags { get; set; } = new List<ItemVm>();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Tag, TagDetailsVm>()
                .ForMember(t => t.Id, map => map.MapFrom(src => src.Id))
                .ForMember(t => t.Name, map => map.MapFrom(src => src.Name))
                .ForMember(t => t.ItemTags, map => map.MapFrom(src => MapToItemVm(src.ItemTags)));
        }

        private static List<ItemVm> MapToItemVm(ICollection<Domain.Model.ItemTag> itemTags)
        {
            var items = new List<ItemVm>();
            if (itemTags is null)
            {
                return items;
            }

            foreach (var itemTag in itemTags)
            {
                if (itemTag is null || itemTag.Item is null)
                {
                    continue;
                }

                items.Add(new ItemVm
                {
                    Id = itemTag?.Item?.Id ?? 0,
                    Name = itemTag?.Item?.Name ?? "",
                    BrandId = itemTag?.Item?.BrandId ?? 0,
                    CurrencyId = itemTag?.Item?.CurrencyId ?? 0,
                    Description = itemTag?.Item.Description,
                    Cost = itemTag?.Item?.Cost ?? 0,
                    Quantity = itemTag?.Item?.Quantity ?? 0,
                    TypeId = itemTag?.Item?.TypeId ?? 0,
                    Warranty = itemTag?.Item?.Warranty ?? "",
                    ItemTags = itemTag?.Item?.ItemTags?.Select(t => new ItemTagVm
                    {
                        TagId = t.TagId
                    })?.ToList() ?? new List<ItemTagVm>()
                });
            }
            return items;
        }
    }

    public class TagDetailsValidation : AbstractValidator<TagDetailsVm>
    {
        public TagDetailsValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
