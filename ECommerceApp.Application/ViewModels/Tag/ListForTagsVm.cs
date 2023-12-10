using ECommerceApp.Application.DTO;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Tag
{
    public class ListForTagsVm
    {
        public List<TagDto> Tags { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForTagsValidation : AbstractValidator<ListForTagsVm>
    {
        public ListForTagsValidation()
        {
            RuleFor(x => x.Tags).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
