using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Services.Items
{
    internal class ItemHandler : IItemHandler
    {
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<ItemHandler> _logger;

        public ItemHandler(IItemRepository itemRepository, ILogger<ItemHandler> logger)
        {
            _itemRepository = itemRepository;
            _logger = logger;
        }

        public void HandleItemsChangesOnOrder(Order orderBeforeChange, Order orderAfterChange)
        {
            if (orderBeforeChange is null && orderAfterChange is null)
            {
                throw new ArgumentException($"{nameof(orderBeforeChange)} and {nameof(orderAfterChange)} cannot be null");
            }

            if (orderBeforeChange is null)
            {
                HandleAddOrder(orderAfterChange);
                return;
            }

            if (orderAfterChange is null)
            {
                HandleDeleteOrder(orderBeforeChange);
                return;
            }

            HandleUpdateOrder(orderBeforeChange, orderAfterChange);
        }

        private void HandleAddOrder(Order orderAfterChange)
        {
            var itemsAdded = GetItemDictionary(orderAfterChange);
            var items = _itemRepository.GetItemsByIds(itemsAdded.Keys);
            foreach (var itemAdded in itemsAdded)
            {
                var item = items.FirstOrDefault(i => i.Id == itemAdded.Key);
                // TODO: multiple errors on front
                HandleAddItem(itemAdded, item, orderAfterChange.Id);
            }
        }

        private void HandleDeleteOrder(Order orderBeforeChange)
        {
            var itemsDeleted = GetItemDictionary(orderBeforeChange);
            var items = _itemRepository.GetItemsByIds(itemsDeleted.Keys);
            foreach ( var itemDeleted in itemsDeleted)
            {
                var item = items.FirstOrDefault(i => i.Id == itemDeleted.Key);
                HandleDeleteItem(itemDeleted, item);
            }
        }

        private void HandleUpdateOrder(Order orderBeforeChange, Order orderAfterChange)
        {
            var itemsOnOrderBefore = GetItemDictionary(orderBeforeChange);
            var itemsOnOrderAfter = GetItemDictionary(orderAfterChange);
            var itemsModifiedKeys = new List<int>(itemsOnOrderBefore.Keys);
            itemsModifiedKeys.AddRange(itemsOnOrderAfter.Keys);
            var items = _itemRepository.GetItemsByIds(itemsModifiedKeys);

            foreach (var itemsBefore in itemsOnOrderBefore)
            {
                var itemAfter = itemsOnOrderAfter.FirstOrDefault(i => i.Key == itemsBefore.Key);
                var item = items.FirstOrDefault(i => i.Id == itemsBefore.Key);
                if (item is null)
                {
                    _logger.LogWarning($"Item with id '{itemsBefore.Key}' was not found");
                    continue;
                }

                if (itemAfter.Value is null)
                {
                    HandleDeleteItem(itemsBefore, item);
                    continue;
                }

                var quantityBefore = GetItemQuantity(itemsBefore);
                var quantityAfter = GetItemQuantity(itemAfter);
                var totalQuantity = quantityAfter - quantityBefore;
                if (totalQuantity == 0)
                {
                    continue;
                }

                if (totalQuantity > 0)
                {
                    if (item.Quantity - totalQuantity < 0)
                    {
                        _logger.LogWarning($"Order with id '{orderBeforeChange.Id}' added with item with id '{item.Id}' that has 0 quantity");
                        throw new BusinessException($"Order with id '{orderBeforeChange.Id}' has item with id '{item.Id}' that cannot be ordered with quantity of '{quantityAfter}', available '{item.Quantity}'", "tooManyItemsQuantityInCart", new Dictionary<string, string> { { "id", $"{item.Id}" }, { "name", item.Name }, { "availableQuantity", $"{item.Quantity}" } });
                    }
                    item.Quantity -= totalQuantity;
                    continue;
                }

                if (totalQuantity < 0)
                {
                    item.Quantity += totalQuantity * -1;
                }
            }

            foreach(var itemsAfter in itemsOnOrderAfter)
            {
                var itemBefore = itemsOnOrderAfter[itemsAfter.Key];
                var item = items.FirstOrDefault(i => i.Id == itemsAfter.Key);
                if (itemBefore is null)
                {
                    HandleAddItem(itemsAfter, item, orderAfterChange.Id);
                }
            }
        }

        private void HandleAddItem(KeyValuePair<int, IEnumerable<OrderItem>> itemsAssociatedWithOrderItems, Item item, int orderId)
        {
            if (item is null)
            {
                _logger.LogWarning($"Item with id '{itemsAssociatedWithOrderItems.Key}' was not found");
                return;
            }
            var quantiyToDelete = GetItemQuantity(itemsAssociatedWithOrderItems);
            if (item.Quantity - quantiyToDelete < 0)
            {
                _logger.LogWarning($"Order with id '{orderId}' added with item with id '{item.Id}' that has 0 quantity");
                throw new BusinessException($"Order with id '{orderId}' has item with id '{item.Id}' that cannot be ordered with quantity of '{quantiyToDelete}', available '{item.Quantity}'", "tooManyItemsQuantityInCart", new Dictionary<string, string> { { "id", $"{item.Id}" }, { "name", item.Name }, { "availableQuantity", $"{item.Quantity}" } });
            }

            item.Quantity -= quantiyToDelete;
            _itemRepository.UpdateItem(item);
        }
        private void HandleDeleteItem(KeyValuePair<int, IEnumerable<OrderItem>> itemsAssociatedWithOrderItems, Item item)
        {
            if (item is null)
            {
                _logger.LogWarning($"Item with id '{itemsAssociatedWithOrderItems.Key}' was not found");
                return;
            }
            var quantiyToAdd = GetItemQuantity(itemsAssociatedWithOrderItems);
            item.Quantity += quantiyToAdd;
            _itemRepository.UpdateItem(item);
        }
        private static int GetItemQuantity(KeyValuePair<int, IEnumerable<OrderItem>> itemsAssociatedWithOrderItems)
        {
            return itemsAssociatedWithOrderItems.Value.Sum(i => i.ItemOrderQuantity);
        }
        private static Dictionary<int, IEnumerable<OrderItem>> GetItemDictionary(Order order)
        {
            if (order.OrderItems is null || !order.OrderItems.Any())
            {
                return new Dictionary<int, IEnumerable<OrderItem>>();
            }
            return order.OrderItems
            .ToLookup(item => item.ItemId, item => item)
                .ToDictionary(group => group.Key, group => group.AsEnumerable());
        }
    }
}
