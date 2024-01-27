using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class TagRepository : ITagRepository
    {
        private readonly Context _context;

        public TagRepository(Context context)
        {
            _context = context;
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

        public List<Tag> GetAllTags()
        {
            return _context.Tags.ToList();
        }

        public bool ExistsById(int tagId)
        {
            return _context.Tags.AsNoTracking().Any(t => t.Id == tagId);
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

        public Tag GetTagById(int id)
        {
            return _context.Tags.FirstOrDefault(t => t.Id == id);
        }

        public List<Tag> GetAllTags(int pageSize, int pageNo, string searchString)
        {
            return _context.Tags
                           .Where(it => it.Name.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Tags
                           .Where(it => it.Name.StartsWith(searchString))
                           .Count();
        }

        public Tag GetTagDetailsById(int id)
        {
            return _context.Tags
                           .Include(it => it.ItemTags)
                           .ThenInclude(i => i.Item)
                           .FirstOrDefault(t => t.Id == id);
        }
    }
}
