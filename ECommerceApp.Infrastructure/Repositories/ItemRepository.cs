using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ItemRepository : IItemRepository
    {
        private readonly Context _context;
        public ItemRepository(Context context)
        {
            _context = context;
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

        public IQueryable<Tag> GetAllTags()
        {
            return _context.Tags;
        }

        public IQueryable<ItemTag> GetAllItemsWithTags()
        {
            return _context.ItemTag.Include(inc => inc.Item);
        }

        public IQueryable<ECommerceApp.Domain.Model.Type> GetAllTypes()
        {
            return _context.Types;
        }

        public IQueryable<Item> GetAllItems()
        {
            return _context.Items;
        }

        public int AddItemBrand(Brand brand)
        {
            _context.Brands.Add(brand);
            _context.SaveChanges();
            return brand.Id;
        }

        public int AddItemType(Domain.Model.Type type)
        {
            _context.Types.Add(type);
            _context.SaveChanges();
            return type.Id;
        }

        public void UpdateItem(Item item)
        {
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

        public void UpdateItemType(Domain.Model.Type type)
        {
            _context.Attach(type);
            _context.Entry(type).Property("Name").IsModified = true;
            _context.SaveChanges();
    }

        public void UpdateItemBrand(Brand brand)
        {
            _context.Attach(brand);
            _context.Entry(brand).Property("Name").IsModified = true;
            _context.SaveChanges();
        }

        public Domain.Model.Type GetItemTypeById(int id)
        {
            var type = _context.Types.FirstOrDefault(it => it.Id == id);
            return type;
        }

        public Brand GetItemBrandById(int id)
        {
            var brand = _context.Brands.FirstOrDefault(it => it.Id == id);
            return brand;
        }

        public void DeleteItemType(int id)
        {
            var itemType = _context.Types.Find(id);

            if (itemType != null)
            {
                _context.Types.Remove(itemType);
                _context.SaveChanges();
            }
        }

        public void DeleteItemBrand(int id)
        {
            var itemBrand = _context.Brands.Find(id);

            if (itemBrand != null)
            {
                _context.Brands.Remove(itemBrand);
                _context.SaveChanges();
            }
        }

        public IQueryable<Brand> GetAllBrands()
        {
            return _context.Brands;
        }

        public int AddItemTag(Tag tag)
        {
            _context.Tags.Add(tag);
            _context.SaveChanges();
            return tag.Id;
        }

        public Tag GetItemTagById(int id)
        {
            var tag = _context.Tags.FirstOrDefault(it => it.Id == id);
            return tag;
        }

        public void UpdateTag(Tag tag)
        {
            _context.Attach(tag);
            _context.Entry(tag).Property("Name").IsModified = true;
            _context.SaveChanges();
        }

        public void DeleteTag(int id)
        {
            var tag = _context.Tags.Find(id);

            if (tag != null)
            {
                _context.Tags.Remove(tag);
                _context.SaveChanges();
            }
        }
    }
}
