using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ITagRepository
    {
        void DeleteTag(int tagId);
        int AddTag(Tag tag);
        bool ExistsById(int tagId);
        void UpdateTag(Tag tag);
        List<Tag> GetAllTags();
        List<Tag> GetAllTags(int pageSize, int pageNo, string searchString);
        List<Tag> GetTagsByIds(IEnumerable<int> ids);
        Tag GetTagById(int id);
        int GetCountBySearchString(string searchString);
        Tag GetTagDetailsById(int id);
    }
}
