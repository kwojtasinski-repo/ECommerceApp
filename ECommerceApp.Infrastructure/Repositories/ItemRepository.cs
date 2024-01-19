using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ItemRepository : GenericRepository<Item>, IItemRepository
    {
        public ItemRepository(Context context) : base(context)
        {
        }

        public void DeleteItem(int itemId)
        {
            var item = _context.Items.Find(itemId);

            if (item != null)
            {
                _context.Items.Remove(item);
                _context.SaveChanges();
            }
        }

        public int AddItem(Item item)
        {
            _context.Items.Add(item);
            _context.SaveChanges();
            return item.Id;
        }

        public IQueryable<Item> GetItemsByTypeId(int typeId)
        {
            return _context.Items.Where(it => it.TypeId == typeId);
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

        public IQueryable<ItemTag> GetAllItemsWithTags()
        {
            return _context.ItemTag.Include(inc => inc.Item);
        }

        public IQueryable<Item> GetAllItems()
        {
            return _context.Items;
        }

        public void UpdateItem(Item item)
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

            _context.Items.Update(item);
            _context.SaveChanges();
        }

        public bool ItemExists(int id)
        {
            var item = _context.Items.Where(i => i.Id == id).AsNoTracking().FirstOrDefault();
            return item != null;
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
    }
}
