using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ListForItemTagsVm
    {
        public List<TagForListVm> Tags { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForItemTagsValidation : AbstractValidator<ListForItemTagsVm>
    {
        public ListForItemTagsValidation()
        {
            RuleFor(x => x.Tags).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
