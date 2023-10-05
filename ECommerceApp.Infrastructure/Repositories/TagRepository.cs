using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class TagRepository : GenericRepository<Tag>, ITagRepository
    {
        public TagRepository(Context context) : base(context)
        {
        }

        public int AddTag(Tag tag)
        {
            _context.Tags.Add(tag);
            _context.SaveChanges();
            return tag.Id;
        }

        public void DeleteTag(int tagId)
        {
            var tag = _context.Tags.Find(tagId);

            if (tag != null)
            {
                _context.Tags.Remove(tag);
                _context.SaveChanges();
            }
        }

        public IQueryable<Tag> GetAllTags()
        {
            var tags = _context.Tags.AsQueryable();
            return tags;
        }

        public Tag GetTagById(int tagId)
        {
            var tag = _context.Tags.Where(t => t.Id == tagId).FirstOrDefault();
            return tag;
        }

        public void UpdateTag(Tag tag)
        {
            _context.Tags.Update(tag);
            _context.SaveChanges();
        }
    }
}
