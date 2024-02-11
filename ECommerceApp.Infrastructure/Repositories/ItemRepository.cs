using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ItemRepository : IItemRepository
    {
        private readonly Context _context;

        public ItemRepository(Context context)
        {
            _context = context;
        }

        public bool DeleteItem(int itemId)
        {
            var item = _context.Items.Find(itemId);
            if (item is null)
            {
                return false;
            }

            _context.Items.Remove(item);
            return _context.SaveChanges() > 0;
        }

        public int AddItem(Item item)
        {
            _context.Items.Add(item);
            _context.SaveChanges();
            return item.Id;
        }

        public Item GetItemById(int itemId)
        {
            var item = _context.Items
                .Include(inc => inc.Brand)
                .Include(inc => inc.Type)
                .Include(inc => inc.Currency)
                .FirstOrDefault(it => it.Id == itemId);
            return item;
        }

        public List<ItemTag> GetAllItemsWithTags(int pageSize, int pageNo, string searchString)
        {
            return _context.ItemTag
                           .Include(inc => inc.Item)
                           .Include(inc => inc.Tag)
                           .Where(it => it.Item.Name.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public List<Item> GetAllItems()
        {
            return _context.Items.ToList();
        }

        public void UpdateItem(Item item)
        {
            if (item.ItemTags is not null && item.ItemTags.Any())
            {
                var newItemTags = new List<ItemTag>(item.ItemTags);
                var currentItemTags = _context.ItemTag.Where(it => it.ItemId == item.Id).AsNoTracking().ToList();

                foreach (var itemTag in newItemTags)
                {
                    if (currentItemTags.Any(it => it.TagId == itemTag.TagId))
                    {
                        continue;
                    }

                    var entry = _context.Entry(itemTag);
                    entry.State = EntityState.Added;
                }

                foreach (var itemTag in currentItemTags)
                {
                    if (newItemTags.Any(it => it.TagId == itemTag.TagId))
                    {
                        continue;
                    }

                    var entry = _context.Entry(itemTag);
                    entry.State = EntityState.Deleted;
                }
            }

            _context.Items.Update(item);
            _context.SaveChanges();
        }

        public bool ExistsById(int id)
        {
            return _context.Items.AsNoTracking().Any(i => i.Id == id);
        }

        public Item GetItemDetailsById(int itemId)
        {
            var item = _context.Items
                .Include(inc => inc.Brand)
                .Include(inc => inc.Type)
                .Include(inc => inc.ItemTags).ThenInclude(inc => inc.Tag)
                .Include(inc => inc.Currency)
                .Include(inc => inc.Images)
                .FirstOrDefault(it => it.Id == itemId);
            return item;
        }

        public int GetCountItemTagsBySearchString(string searchString)
        {
            return _context.ItemTag
                           .Include(inc => inc.Item)
                           .Include(inc => inc.Tag)
                           .Where(it => it.Item.Name.StartsWith(searchString))
                           .Count();
        }

        public List<Item> GetItemInfos()
        {
            return _context.Items
                           .Select(i => new Item
                           {
                               Id = i.Id,
                               Name = i.Name,
                               Cost = i.Cost
                           })
                           .ToList();
        }

        public List<Item> GetAllItems(int pageSize, int pageNo, string searchString)
        {
            return _context.Items
                           .Where(i => i.Name.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Items
                           .Where(i => i.Name.StartsWith(searchString))
                           .Count();
        }

        public List<Item> GetItemsByIds(IEnumerable<int> ids)
        {
            return _context.Items
                            .Where(i => ids.Contains(i.Id))
                            .ToList();
        }
    }
}
