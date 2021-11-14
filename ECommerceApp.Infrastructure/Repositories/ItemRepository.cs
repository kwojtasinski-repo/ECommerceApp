using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                .Include(inc => inc.ItemTags).ThenInclude(inc => inc.Tag)
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
            //Backup new item collection tags before clearing 
            var newItemTags = item.ItemTags;
            item.ItemTags = new List<ItemTag>();

            _context.Entry(item).State = EntityState.Modified;

            //Load item's current tags from DB and remove them
            _context.Entry(item).Collection(i => i.ItemTags).Load();
            var currentItems = item.ItemTags.ToList();
            item.ItemTags.Clear();

            foreach (var itemTag in newItemTags)
            {
                var currentItem = currentItems.SingleOrDefault(it => it.TagId == itemTag.TagId);
                if (currentItem != null)
                {
                    item.ItemTags.Add(currentItem);
                }
                else
                {
                    _context.ItemTag.Add(itemTag);
                    item.ItemTags.Add(itemTag);
                }
            }

            _context.Attach(item);
            _context.Entry(item).Property("Name").IsModified = true;
            _context.Entry(item).Property("Cost").IsModified = true;
            _context.Entry(item).Property("Description").IsModified = true;
            _context.Entry(item).Property("Warranty").IsModified = true;
            _context.Entry(item).Property("Quantity").IsModified = true;
            _context.Entry(item).Property("BrandId").IsModified = true;
            _context.Entry(item).Property("TypeId").IsModified = true;
            _context.Entry(item).Collection("OrderItems").IsModified = true;
           _context.Entry(item).Collection("ItemTags").IsModified = true;

            _context.SaveChanges();
        }
    }
}
