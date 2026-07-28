using System;
using System.Collections.Generic;
using ECommerceApp.Application.Catalog.Products.Messages;

namespace ECommerceApp.Application.Messaging
{
    /// <summary>
    /// Explicit, reviewable registry mapping outbox message types to short, stable string keys
    /// used as <c>OutboxMessage.MessageTypeKey</c>. Deliberately NOT reflection/assembly-scan based.
    /// </summary>
    public static class MessageTypeRegistry
    {
        private static readonly Dictionary<Type, string> KeysByType = new();
        private static readonly Dictionary<string, Type> TypesByKey = new();

        static MessageTypeRegistry()
        {
            // Register message types as retrofits are applied in Phase 3.
            // Keys are short, stable strings used in OutboxMessage.MessageTypeKey.
            Register(typeof(ProductUpdated), "catalog.product.updated");
            Register(typeof(ProductPublished), "catalog.product.published");
            Register(typeof(ProductUnpublished), "catalog.product.unpublished");
        }

        internal static void Register(Type messageType, string key)
        {
            KeysByType.Add(messageType, key);
            TypesByKey.Add(key, messageType);
        }

        public static string KeyFor(Type messageType)
        {
            if (KeysByType.TryGetValue(messageType, out var key))
                return key;

            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' is not registered in {nameof(MessageTypeRegistry)}. " +
                "Add an explicit Register(...) entry before publishing this type via the outbox.");
        }

        public static Type TypeFor(string key)
        {
            if (TypesByKey.TryGetValue(key, out var type))
                return type;

            throw new InvalidOperationException(
                $"Outbox message type key '{key}' is not registered in {nameof(MessageTypeRegistry)}. " +
                "This indicates a deploy-order mismatch or a removed message type — investigate, do not swallow.");
        }
    }
}
