using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace ECommerceApp.UnitTests.Architecture
{
    using static ArchRuleDefinition;

    /// <summary>
    /// Verifies that bounded context boundaries are respected across Domain, Application and Infrastructure layers.
    /// Each BC may only depend on its own namespace tree, the Shared kernel, and the Messaging infrastructure.
    /// Cross-BC communication is allowed ONLY through:
    ///   - Message contracts (Application.{BC}.Messages) consumed via IMessageHandler
    ///   - Contract interfaces (Application.{BC}.Contracts) implemented by Infrastructure adapters
    /// Direct service-to-service calls across BC boundaries are violations.
    /// </summary>
    public class BoundedContextDependencyTests
    {
        private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
            .LoadAssemblies(
                Assembly.Load("ECommerceApp.Domain"),
                Assembly.Load("ECommerceApp.Application"),
                Assembly.Load("ECommerceApp.Infrastructure")
            )
            .Build();

        // ── Helper: match types in a namespace AND all sub-namespaces ───────────
        //    ResideInNamespace in ArchUnitNET 0.13.x is exact-match only.

        private static IObjectProvider<IType> InNsTree(string ns, string alias) =>
            Types().That().FollowCustomPredicate(
                t => t.FullName.StartsWith(ns + "."),
                $"reside in namespace tree \"{ns}\"")
            .As(alias);

        // ── Shared layers (allowed everywhere) ──────────────────────────────────

        private static readonly IObjectProvider<IType> SharedDomain =
            InNsTree("ECommerceApp.Domain.Shared", "Domain.Shared");

        private static readonly IObjectProvider<IType> Messaging =
            InNsTree("ECommerceApp.Application.Messaging", "Application.Messaging");

        private static readonly IObjectProvider<IType> InfrastructureMessaging =
            InNsTree("ECommerceApp.Infrastructure.Messaging", "Infrastructure.Messaging");

        private static readonly IObjectProvider<IType> SharedApplication =
            Types().That().FollowCustomPredicate(
                t => t.FullName.StartsWith("ECommerceApp.Application.Mapping.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.Exceptions.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.Interfaces.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.FileManager.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.External.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.Utils.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.Abstracts.") ||
                     t.FullName.StartsWith("ECommerceApp.Application.ViewModels.BaseVm"),
                "reside in shared application infrastructure")
            .As("Application.Shared");

        // ── Domain layer BCs ────────────────────────────────────────────────────

        private static readonly IObjectProvider<IType> DomainOrders =
            InNsTree("ECommerceApp.Domain.Sales.Orders", "Domain.Sales.Orders");

        private static readonly IObjectProvider<IType> DomainPayments =
            InNsTree("ECommerceApp.Domain.Sales.Payments", "Domain.Sales.Payments");

        private static readonly IObjectProvider<IType> DomainCoupons =
            InNsTree("ECommerceApp.Domain.Sales.Coupons", "Domain.Sales.Coupons");

        private static readonly IObjectProvider<IType> DomainFulfillment =
            InNsTree("ECommerceApp.Domain.Sales.Fulfillment", "Domain.Sales.Fulfillment");

        private static readonly IObjectProvider<IType> DomainInventory =
            InNsTree("ECommerceApp.Domain.Inventory.Availability", "Domain.Inventory.Availability");

        private static readonly IObjectProvider<IType> DomainPresale =
            InNsTree("ECommerceApp.Domain.Presale.Checkout", "Domain.Presale.Checkout");

        private static readonly IObjectProvider<IType> DomainCatalog =
            InNsTree("ECommerceApp.Domain.Catalog", "Domain.Catalog");

        private static readonly IObjectProvider<IType> DomainAccountProfile =
            InNsTree("ECommerceApp.Domain.AccountProfile", "Domain.AccountProfile");

        private static readonly IObjectProvider<IType> DomainCurrencies =
            InNsTree("ECommerceApp.Domain.Supporting.Currencies", "Domain.Supporting.Currencies");

        private static readonly IObjectProvider<IType> DomainTimeManagement =
            InNsTree("ECommerceApp.Domain.Supporting.TimeManagement", "Domain.Supporting.TimeManagement");

        private static readonly IObjectProvider<IType> DomainIdentity =
            InNsTree("ECommerceApp.Domain.Identity.IAM", "Domain.Identity.IAM");

        // ── Application layer BCs ───────────────────────────────────────────────

        private static readonly IObjectProvider<IType> AppOrders =
            InNsTree("ECommerceApp.Application.Sales.Orders", "Application.Sales.Orders");

        private static readonly IObjectProvider<IType> AppPayments =
            InNsTree("ECommerceApp.Application.Sales.Payments", "Application.Sales.Payments");

        private static readonly IObjectProvider<IType> AppCoupons =
            InNsTree("ECommerceApp.Application.Sales.Coupons", "Application.Sales.Coupons");

        private static readonly IObjectProvider<IType> AppFulfillment =
            InNsTree("ECommerceApp.Application.Sales.Fulfillment", "Application.Sales.Fulfillment");

        private static readonly IObjectProvider<IType> AppInventory =
            InNsTree("ECommerceApp.Application.Inventory.Availability", "Application.Inventory.Availability");

        private static readonly IObjectProvider<IType> AppPresale =
            InNsTree("ECommerceApp.Application.Presale.Checkout", "Application.Presale.Checkout");

        private static readonly IObjectProvider<IType> AppCatalog =
            InNsTree("ECommerceApp.Application.Catalog", "Application.Catalog");

        private static readonly IObjectProvider<IType> AppAccountProfile =
            InNsTree("ECommerceApp.Application.AccountProfile", "Application.AccountProfile");

        private static readonly IObjectProvider<IType> AppCurrencies =
            InNsTree("ECommerceApp.Application.Supporting.Currencies", "Application.Supporting.Currencies");

        private static readonly IObjectProvider<IType> AppTimeManagement =
            InNsTree("ECommerceApp.Application.Supporting.TimeManagement", "Application.Supporting.TimeManagement");

        private static readonly IObjectProvider<IType> AppIdentity =
            InNsTree("ECommerceApp.Application.Identity.IAM", "Application.Identity.IAM");

        // ── Message contracts (cross-BC consumption allowed) ────────────────────

        private static readonly IObjectProvider<IType> OrderMessages =
            InNsTree("ECommerceApp.Application.Sales.Orders.Messages", "Orders.Messages");

        private static readonly IObjectProvider<IType> PaymentMessages =
            InNsTree("ECommerceApp.Application.Sales.Payments.Messages", "Payments.Messages");

        private static readonly IObjectProvider<IType> CouponMessages =
            InNsTree("ECommerceApp.Application.Sales.Coupons.Messages", "Coupons.Messages");

        private static readonly IObjectProvider<IType> FulfillmentMessages =
            InNsTree("ECommerceApp.Application.Sales.Fulfillment.Messages", "Fulfillment.Messages");

        private static readonly IObjectProvider<IType> CatalogMessages =
            InNsTree("ECommerceApp.Application.Catalog.Products.Messages", "Catalog.Messages");

        private static readonly IObjectProvider<IType> InventoryMessages =
            InNsTree("ECommerceApp.Application.Inventory.Availability.Messages", "Inventory.Messages");

        // ════════════════════════════════════════════════════════════════════════
        // Domain layer isolation — each domain BC depends only on itself + Shared
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Domain_Orders_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainOrders)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainOrders)
                        .And().AreNot(SharedDomain))
                .Because("Orders domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Payments_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainPayments)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainPayments)
                        .And().AreNot(SharedDomain))
                .Because("Payments domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Coupons_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainCoupons)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainCoupons)
                        .And().AreNot(SharedDomain))
                .Because("Coupons domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Fulfillment_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainFulfillment)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainFulfillment)
                        .And().AreNot(SharedDomain))
                .Because("Fulfillment domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Inventory_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainInventory)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainInventory)
                        .And().AreNot(SharedDomain))
                .Because("Inventory domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Presale_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainPresale)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainPresale)
                        .And().AreNot(SharedDomain))
                .Because("Presale domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Catalog_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainCatalog)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainCatalog)
                        .And().AreNot(SharedDomain))
                .Because("Catalog domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_AccountProfile_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainAccountProfile)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainAccountProfile)
                        .And().AreNot(SharedDomain))
                .Because("AccountProfile domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Currencies_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainCurrencies)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainCurrencies)
                        .And().AreNot(SharedDomain))
                .Because("Currencies domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_TimeManagement_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainTimeManagement)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainTimeManagement)
                        .And().AreNot(SharedDomain))
                .Because("TimeManagement domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_Identity_ShouldNotDependOnOtherBCs()
        {
            Types().That().Are(DomainIdentity)
                .Should().NotDependOnAny(
                    Types().That().AreNot(DomainIdentity)
                        .And().AreNot(SharedDomain))
                .Because("Identity domain must be self-contained (+ Shared kernel)")
                .Check(Architecture);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Application layer — BCs depend on own domain + Shared + Messaging +
        // cross-BC message contracts (for handlers subscribing to foreign events)
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void App_Orders_ShouldOnlyDependOnOwnDomainAndMessageContracts()
        {
            // Orders handlers consume: PaymentConfirmed, PaymentExpired, CouponApplied,
            // CouponRemovedFromOrder, RefundApproved — these are message contract refs
            Types().That().Are(AppOrders)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppOrders)
                        .And().AreNot(DomainOrders)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(PaymentMessages)     // OrderPaymentConfirmedHandler, OrderPaymentExpiredHandler
                        .And().AreNot(CouponMessages)      // OrderCouponAppliedHandler, OrderCouponRemovedHandler
                        .And().AreNot(FulfillmentMessages)  // OrderRefundApprovedHandler
                        .And().AreNot(AppTimeManagement))   // SnapshotOrderItemsJob implements IScheduledTask
                .Because("Orders application must not depend on other BCs except via message contracts and TimeManagement for jobs")
                .Check(Architecture);
        }

        [Fact]
        public void App_Payments_ShouldOnlyDependOnOwnDomainAndMessageContracts()
        {
            // Payments handlers consume: OrderPlaced, RefundApproved
            Types().That().Are(AppPayments)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppPayments)
                        .And().AreNot(DomainPayments)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(OrderMessages)       // OrderPlacedHandler
                        .And().AreNot(FulfillmentMessages)  // PaymentRefundApprovedHandler
                        .And().AreNot(AppTimeManagement))   // PaymentWindowExpiredJob uses IScheduledTask/IDeferredJobScheduler
                .Because("Payments application must not depend on other BCs except via message contracts")
                .Check(Architecture);
        }

        [Fact]
        public void App_Coupons_ShouldOnlyDependOnOwnDomainAndMessageContracts()
        {
            // Coupons handlers consume: OrderCancelled, ProductNameChanged,
            // CategoryNameChanged, TagNameChanged
            Types().That().Are(AppCoupons)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppCoupons)
                        .And().AreNot(DomainCoupons)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(OrderMessages)       // CouponsOrderCancelledHandler
                        .And().AreNot(CatalogMessages))    // ProductNameChanged/CategoryNameChanged/TagNameChangedHandler
                .Because("Coupons application must not depend on other BCs except via message contracts")
                .Check(Architecture);
        }

        [Fact]
        public void App_Fulfillment_ShouldOnlyDependOnOwnDomainAndMessageContracts()
        {
            Types().That().Are(AppFulfillment)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppFulfillment)
                        .And().AreNot(DomainFulfillment)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging))
                .Because("Fulfillment application must not depend on other BCs except via message contracts")
                .Check(Architecture);
        }

        [Fact]
        public void App_Inventory_ShouldOnlyDependOnOwnDomainAndMessageContracts()
        {
            // Inventory handlers consume: OrderPlaced, OrderCancelled,
            // PaymentConfirmed, ProductPublished, ProductUnpublished, ProductDiscontinued,
            // RefundApproved (from Payments), ShipmentDelivered/Failed/PartiallyDelivered (from Fulfillment)
            Types().That().Are(AppInventory)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppInventory)
                        .And().AreNot(DomainInventory)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(OrderMessages)       // OrderPlacedHandler, OrderCancelledHandler
                        .And().AreNot(PaymentMessages)     // PaymentConfirmedHandler, RefundApprovedHandler
                        .And().AreNot(CatalogMessages)     // ProductPublished/Unpublished/DiscontinuedHandler
                        .And().AreNot(FulfillmentMessages)  // ShipmentDelivered/Failed/PartiallyDeliveredHandler
                        .And().AreNot(AppTimeManagement))  // StockAdjustmentJob, PaymentWindowTimeoutJob
                .Because("Inventory application must not depend on other BCs except via message contracts")
                .Check(Architecture);
        }

        [Fact]
        public void App_Presale_ShouldOnlyDependOnOwnDomainAndMessageContracts()
        {
            // Presale handlers consume: OrderPlaced, StockAvailabilityChanged
            Types().That().Are(AppPresale)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppPresale)
                        .And().AreNot(DomainPresale)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(OrderMessages)       // OrderPlacedHandler
                        .And().AreNot(InventoryMessages)   // StockAvailabilityChangedHandler
                        .And().AreNot(AppTimeManagement))  // SoftReservationExpiredJob
                .Because("Presale application must not depend on other BCs except via message contracts")
                .Check(Architecture);
        }

        [Fact]
        public void App_Catalog_ShouldOnlyDependOnOwnDomain()
        {
            Types().That().Are(AppCatalog)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppCatalog)
                        .And().AreNot(DomainCatalog)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging))
                .Because("Catalog application must not depend on other BCs")
                .Check(Architecture);
        }

        [Fact]
        public void App_AccountProfile_ShouldOnlyDependOnOwnDomain()
        {
            Types().That().Are(AppAccountProfile)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppAccountProfile)
                        .And().AreNot(DomainAccountProfile)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging))
                .Because("AccountProfile application must not depend on other BCs")
                .Check(Architecture);
        }

        [Fact]
        public void App_Currencies_ShouldOnlyDependOnOwnDomain()
        {
            Types().That().Are(AppCurrencies)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppCurrencies)
                        .And().AreNot(DomainCurrencies)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(AppTimeManagement))  // CurrencyRateSyncTask implements IScheduledTask
                .Because("Currencies application must not depend on other BCs")
                .Check(Architecture);
        }

        [Fact]
        public void App_Identity_ShouldOnlyDependOnOwnDomain()
        {
            Types().That().Are(AppIdentity)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppIdentity)
                        .And().AreNot(DomainIdentity)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging)
                        .And().AreNot(AppTimeManagement))   // RefreshTokenCleanupTask implements IScheduledTask
                .Because("Identity application must not depend on other BCs")
                .Check(Architecture);
        }

        [Fact]
        public void App_TimeManagement_ShouldOnlyDependOnOwnDomain()
        {
            Types().That().Are(AppTimeManagement)
                .Should().NotDependOnAny(
                    Types().That().AreNot(AppTimeManagement)
                        .And().AreNot(DomainTimeManagement)
                        .And().AreNot(SharedDomain)
                        .And().AreNot(SharedApplication)
                        .And().AreNot(Messaging))
                .Because("TimeManagement application must not depend on other BCs")
                .Check(Architecture);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Domain layer must not reference Application or Infrastructure
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Domain_ShouldNotDependOnApplication()
        {
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Domain."),
                    "reside in ECommerceApp.Domain tree")
                .Should().NotDependOnAny(
                    Types().That().FollowCustomPredicate(
                        t => t.FullName.StartsWith("ECommerceApp.Application."),
                        "reside in ECommerceApp.Application tree"))
                .Because("Domain must not depend on Application (dependency inversion)")
                .Check(Architecture);
        }

        [Fact]
        public void Domain_ShouldNotDependOnInfrastructure()
        {
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Domain."),
                    "reside in ECommerceApp.Domain tree")
                .Should().NotDependOnAny(
                    Types().That().FollowCustomPredicate(
                        t => t.FullName.StartsWith("ECommerceApp.Infrastructure."),
                        "reside in ECommerceApp.Infrastructure tree"))
                .Because("Domain must not depend on Infrastructure (dependency inversion)")
                .Check(Architecture);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Application layer must not reference Infrastructure
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Application_ShouldNotDependOnInfrastructure()
        {
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Application."),
                    "reside in ECommerceApp.Application tree")
                .Should().NotDependOnAny(
                    Types().That().FollowCustomPredicate(
                        t => t.FullName.StartsWith("ECommerceApp.Infrastructure."),
                        "reside in ECommerceApp.Infrastructure tree"))
                .Because("Application must not depend on Infrastructure (dependency inversion)")
                .Check(Architecture);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Shared kernel must not depend on any BC
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Shared_ShouldNotDependOnAnyBC()
        {
            Types().That().Are(SharedDomain)
                .Should().NotDependOnAny(
                    Types().That().AreNot(SharedDomain))
                .Because("Shared kernel must be dependency-free (leaf node)")
                .Check(Architecture);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Infrastructure adapters — verify cross-BC wiring only happens in adapters
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Infrastructure_Fulfillment_CrossBcAccess_OnlyInAdapters()
        {
            // Only Adapters folder should reference Orders services
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Infrastructure.Sales.Fulfillment.") &&
                         !t.FullName.StartsWith("ECommerceApp.Infrastructure.Sales.Fulfillment.Adapters."),
                    "reside in Infrastructure.Sales.Fulfillment tree but not in Adapters")
                .Should().NotDependOnAny(
                    Types().That().Are(AppOrders))
                .Because("Only Fulfillment adapters should depend on Orders services — not repositories or DbContext")
                .Check(Architecture);
        }

        [Fact]
        public void Infrastructure_Coupons_CrossBcAccess_OnlyInAdapters()
        {
            // Adapters reference: IOrderService (CompletedOrderCounterAdapter),
            //                     IStockService (StockAvailabilityCheckerAdapter)
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Infrastructure.Sales.Coupons.") &&
                         !t.FullName.StartsWith("ECommerceApp.Infrastructure.Sales.Coupons.Adapters."),
                    "reside in Infrastructure.Sales.Coupons tree but not in Adapters")
                .Should().NotDependOnAny(
                    Types().That().Are(AppOrders)
                        .Or().Are(AppInventory))
                .Because("Only Coupons adapters should depend on Orders/Inventory services — not repositories or DbContext")
                .Check(Architecture);
        }

        [Fact]
        public void Infrastructure_Presale_CrossBcAccess_OnlyInAdapters()
        {
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Infrastructure.Presale.Checkout.") &&
                         !t.FullName.StartsWith("ECommerceApp.Infrastructure.Presale.Checkout.Adapters."),
                    "reside in Infrastructure.Presale.Checkout tree but not in Adapters")
                .Should().NotDependOnAny(
                    Types().That().Are(AppCatalog)
                        .Or().Are(AppInventory)
                        .Or().Are(AppOrders))
                .Because("Only Presale adapters should depend on Catalog/Inventory/Orders services")
                .Check(Architecture);
        }

        [Fact]
        public void Infrastructure_Orders_CrossBcAccess_OnlyInAdapters()
        {
            // OrderCustomerResolver adapter reads legacy Context for customer data
            Types().That().FollowCustomPredicate(
                    t => t.FullName.StartsWith("ECommerceApp.Infrastructure.Sales.Orders.") &&
                         !t.FullName.StartsWith("ECommerceApp.Infrastructure.Sales.Orders.Adapters."),
                    "reside in Infrastructure.Sales.Orders tree but not in Adapters")
                .Should().NotDependOnAny(
                    Types().That().Are(AppAccountProfile)
                        .Or().Are(AppCatalog)
                        .Or().Are(AppInventory))
                .Because("Only Orders adapters should depend on other BC services — not repositories or DbContext")
                .Check(Architecture);
        }
    }
}
