using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class ItemController : Controller
    {
        public IActionResult ItemsView()
        {
            List<Item> items = new List<Item>();
            items.Add(new Item() { ItemId = 1, ItemName = "T-Shirt", ItemTypeName = "Clothing", ItemCost = 50.0, ItemDescription = "Podkoszulka marki Gucci", ItemQuantity = 50 });
            items.Add(new Item() { ItemId = 2, ItemName = "Apple", ItemTypeName = "Grocery", ItemCost = 0.50, ItemDescription = "Jablko swieze", ItemQuantity = 120 });
            items.Add(new Item() { ItemId = 3, ItemName = "Cup", ItemTypeName = "KitchenUtensils", ItemCost = 15.0, ItemDescription = "Kubek ze sportowymi samochodami", ItemQuantity = 20 });
            items.Add(new Item() { ItemId = 4, ItemName = "Arduino Leonardo", ItemTypeName = "ElectricalDevices", ItemCost = 90.0, ItemDescription = "Arduino Leonardo z mikrokontrolerem AVR Atmega32U4. Posiada 32 kB pamięci Flash, 2,5 kB RAM, 20 cyfrowych wejść/wyjść", ItemQuantity = 500 });

            return View(items);
        }
    }
}
