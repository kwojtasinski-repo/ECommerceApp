using ECommerceApp.Application.DTO;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Type
{
    public class ListForTypeVm
    {
        public List<TypeDto> Types { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForTypeVmValidation : AbstractValidator<ListForTypeVm>
    {
        public ListForTypeVmValidation()
        {
            RuleFor(x => x.Types).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
