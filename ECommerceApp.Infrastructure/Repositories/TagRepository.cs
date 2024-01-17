using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

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

        public List<Tag> GetTagsByIds(IEnumerable<int> ids)
        {
            return _context.Tags
                        .Where(t => ids.Any(id => t.Id == id))
                        .ToList();
        }

        public void UpdateTag(Tag tag)
        {
            _context.Tags.Update(tag);
            _context.SaveChanges();
        }
    }
}
