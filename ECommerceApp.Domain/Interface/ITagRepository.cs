using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ITagRepository : IGenericRepository<Tag>
    {
        void DeleteTag(int tagId);
        int AddTag(Tag tag);
        Tag GetTagById(int tagId);
        IQueryable<Tag> GetAllTags();
        void UpdateTag(Tag tag);
    }
}
