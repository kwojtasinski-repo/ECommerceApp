using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Tag;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Items
{
    public interface ITagService
    {
        int AddTag(TagDto model);
        TagDetailsVm GetTagDetails(int id);
        TagDto GetTagById(int id);
        bool UpdateTag(TagDto model);
        IEnumerable<TagDto> GetTags();
        ListForTagsVm GetTags(int pageSize, int pageNo, string searchString);
        bool TagExists(int id);
        bool DeleteTag(int id);
    }
}
