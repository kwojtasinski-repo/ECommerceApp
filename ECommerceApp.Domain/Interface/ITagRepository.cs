using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface ITagRepository : IGenericRepository<Tag>
    {
        void DeleteTag(int tagId);
        int AddTag(Tag tag);
        Tag GetTagById(int tagId);
        IQueryable<Tag> GetAllTags();
        void UpdateTag(Tag tag);
        List<Tag> GetTagsByIds(IEnumerable<int> ids);
    }
}
