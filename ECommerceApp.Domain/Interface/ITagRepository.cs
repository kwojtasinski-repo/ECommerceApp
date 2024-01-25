using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface ITagRepository
    {
        void DeleteTag(int tagId);
        int AddTag(Tag tag);
        bool ExistsById(int tagId);
        IQueryable<Tag> GetAllTags();
        void UpdateTag(Tag tag);
        List<Tag> GetTagsByIds(IEnumerable<int> ids);
        Tag GetTagById(int id);
    }
}
