using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ListForItemWithTagsVm
    {
        public List<ItemsTagsVm> ItemTags { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForItemWithTagsValidation : AbstractValidator<ListForItemWithTagsVm>
    {
        public ListForItemWithTagsValidation()
        {
            RuleFor(x => x.ItemTags).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
