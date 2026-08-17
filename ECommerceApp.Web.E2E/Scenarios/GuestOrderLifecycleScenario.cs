using ECommerceApp.Web.E2E.PageObjects;
using Microsoft.Playwright;
using Shouldly;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Scenarios
{
    public sealed class GuestOrderLifecycleScenario
    {
        /// <summary>
        /// Places an anonymous guest order and returns only its id — the minimal building block for a
        /// cross-guest isolation check, where the test needs two independently-placed orders and nothing
        /// past checkout (no payment, no promotion) from either.
        /// </summary>
        public async Task<int> ExecuteAnonymousCheckoutAsync(IPage guestPage, string baseAddress, int productId)
        {
            var email = $"guest-{Guid.NewGuid():N}@example.com";
            var summary = await PlaceAnonymousOrderAsync(guestPage, baseAddress, productId, email);
            return await summary.GetOrderIdAsync();
        }

        public async Task<int> ExecuteAnonymousCheckoutAndPromotionAsync(
            IPage guestPage,
            string baseAddress,
            int productId)
        {
            var email = $"guest-{Guid.NewGuid():N}@example.com";
            var summary = await PlaceAnonymousOrderAsync(guestPage, baseAddress, productId, email);
            var orderId = await summary.GetOrderIdAsync();
            var summaryUrl = guestPage.Url;

            var payment = await summary.OpenPaymentAsync();
            await payment.ConfirmPaymentAsync();

            await guestPage.GotoAsync(summaryUrl);
            await guestPage.Locator("#password").FillAsync("GuestPass@2026");
            await guestPage.GetByRole(AriaRole.Button, new() { Name = "Utwórz konto" }).ClickAsync();
            await guestPage.WaitForURLAsync("**/Presale/Checkout/Summary/*");
            (await guestPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(1);

            return orderId;
        }

        /// <summary>
        /// ADR-0030 Phase 9 superseded Phase 7/8's admin-Backoffice-assisted recovery (a magic link at
        /// <c>/Identity/Account/Login?guestOrder=</c>, redeemed via <c>RedeemRecovery</c>) with a fully
        /// self-service flow: the guest re-proves ownership by email + a one-time code, entered by hand
        /// on the unified order-lookup page (<c>CheckoutController.Order</c>/<c>RequestOrderAccess</c>/
        /// <c>ConfirmOrderAccess</c>) — no admin action is needed any more. The admin Backoffice
        /// verification page is still used here only as the stand-in "mailbox": this repo simulates SMTP,
        /// so the code is read from the same admin-visible pending-codes list a real email would have
        /// carried, per this repo's established SMTP-is-simulated-test-via-DB convention — not because an
        /// admin does anything with it.
        /// </summary>
        public async Task<int> ExecuteAnonymousSelfServiceRecoveryAsync(
            IPage guestPage,
            IPage adminPage,
            string baseAddress,
            int productId)
        {
            var email = $"recovery-{Guid.NewGuid():N}@example.com";
            var summary = await PlaceAnonymousOrderAsync(guestPage, baseAddress, productId, email);
            var orderId = await summary.GetOrderIdAsync();

            await guestPage.Context.ClearCookiesAsync();
            var lookup = await OrderAccessLookupPage.NavigateAsync(guestPage, baseAddress, orderId);
            await lookup.RequestAccessAsync(email);

            await adminPage.GotoAsync($"{baseAddress}/Backoffice/GuestVerification");
            var codeCell = adminPage.Locator("tbody tr")
                .Filter(new LocatorFilterOptions { HasText = orderId.ToString() })
                .Locator("code")
                .First;
            var code = (await codeCell.InnerTextAsync()).Trim();
            code.ShouldNotBeNullOrWhiteSpace();

            var confirmedSummary = await lookup.ConfirmAccessAsync(code);
            await confirmedSummary.ShouldConfirmOrderAsync();
            (await confirmedSummary.GetOrderIdAsync()).ShouldBe(orderId);

            return orderId;
        }

        /// <summary>
        /// Places a single-product order for an already-authenticated registered customer and returns
        /// only its id — the registered-account counterpart to
        /// <see cref="ExecuteAnonymousCheckoutAsync"/>, for tests that need a real (non-guest) order to
        /// cross-check a guest against, without the full two-item/fulfillment ceremony of
        /// <see cref="ExecuteAsync"/>. Does not create a browser context or log in — same rule as
        /// <see cref="ExecuteAsync"/>.
        /// </summary>
        public async Task<int> ExecuteRegisteredCustomerCheckoutAsync(
            IPage customerPage,
            string baseAddress,
            int productId)
        {
            var storefront = await StorefrontPage.NavigateAsync(customerPage, baseAddress);
            var product = await storefront.OpenProductAsync(productId);
            await product.AddToCartAsync(productId, 1);

            var cart = await CartPage.NavigateAsync(customerPage, baseAddress);
            var orderForm = await cart.ProceedToOrderAsync();
            await orderForm.FillCustomerAsync();
            var summary = await orderForm.SubmitAsync();
            await summary.ShouldConfirmOrderAsync();
            return await summary.GetOrderIdAsync();
        }

        /// <summary>
        /// The anonymous counterpart to <see cref="ExecuteAsync"/>: an unauthenticated guest checks
        /// out (no login, no account creation) and the order still runs the full admin fulfillment
        /// path through to delivery. This is the "cart to delivery" coverage for ADR-0030 guest
        /// checkout that <see cref="ExecuteAnonymousCheckoutAndPromotionAsync"/> deliberately stops
        /// short of (it proves account promotion instead).
        /// </summary>
        public async Task<OrderLifecycleResult> ExecuteAnonymousCheckoutThroughDeliveryAsync(
            IPage guestPage,
            IPage adminPage,
            string baseAddress,
            int productId)
        {
            var email = $"guest-delivery-{Guid.NewGuid():N}@example.com";
            var summary = await PlaceAnonymousOrderAsync(guestPage, baseAddress, productId, email);
            var orderId = await summary.GetOrderIdAsync();

            var payment = await summary.OpenPaymentAsync();
            await payment.ConfirmPaymentAsync();

            var (orderStatusAfterPayment, finalShipmentStatus) =
                await FulfillThroughDeliveryAsync(adminPage, baseAddress, orderId);

            return new OrderLifecycleResult(
                orderId,
                orderStatusAfterPayment == "PaymentConfirmed",
                finalShipmentStatus);
        }

        public async Task<OrderLifecycleResult> ExecuteAsync(
            IPage customerPage,
            IPage adminPage,
            string baseAddress,
            int firstProductId,
            int secondProductId)
        {
            return await ExecuteAsync(
                customerPage,
                adminPage,
                baseAddress,
                firstProductId,
                secondProductId,
                useStorefrontListing: false);
        }

        public async Task<OrderLifecycleResult> ExecuteThroughStorefrontListingAsync(
            IPage customerPage,
            IPage adminPage,
            string baseAddress,
            int firstProductId,
            int secondProductId)
        {
            return await ExecuteAsync(
                customerPage,
                adminPage,
                baseAddress,
                firstProductId,
                secondProductId,
                useStorefrontListing: true);
        }

        private async Task<OrderLifecycleResult> ExecuteAsync(
            IPage customerPage,
            IPage adminPage,
            string baseAddress,
            int firstProductId,
            int secondProductId,
            bool useStorefrontListing)
        {
            StorefrontPage storefront = await StorefrontPage.NavigateAsync(customerPage, baseAddress);
            IProductDetailsPage firstProduct = useStorefrontListing
                ? await storefront.OpenProductAsync("E2E Lifecycle Product A")
                : await storefront.OpenProductAsync(firstProductId);
            await firstProduct.AddToCartAsync(firstProductId, 2);

            storefront = await StorefrontPage.NavigateAsync(customerPage, baseAddress);
            IProductDetailsPage secondProduct = useStorefrontListing
                ? await storefront.OpenProductAsync("E2E Lifecycle Product B")
                : await storefront.OpenProductAsync(secondProductId);
            await secondProduct.AddToCartAsync(secondProductId, 3);

            ICartPage cart = await CartPage.NavigateAsync(customerPage, baseAddress);
            cart = await cart.ShouldContainProductAsync("E2E Lifecycle Product A", 2);
            await cart.ShouldContainProductAsync("E2E Lifecycle Product B", 3);

            IPlaceOrderPage orderForm = await cart.ProceedToOrderAsync();
            orderForm = await orderForm.FillCustomerAsync();
            IOrderSummaryPage summary = await orderForm.SubmitAsync();
            await summary.ShouldConfirmOrderAsync();

            var orderId = await summary.GetOrderIdAsync();
            IPaymentPage payment = await summary.OpenPaymentAsync();
            await payment.ConfirmPaymentAsync();

            var (orderStatusAfterPayment, finalShipmentStatus) =
                await FulfillThroughDeliveryAsync(adminPage, baseAddress, orderId);

            return new OrderLifecycleResult(orderId, orderStatusAfterPayment == "PaymentConfirmed", finalShipmentStatus);
        }

        /// <summary>
        /// Drives the admin side of fulfillment (create/dispatch/deliver shipment) for an order that
        /// has already been paid, and reads the order status from the admin's page rather than
        /// assuming it — this is what proves the customer's payment actually reached the read model
        /// the back office works from. Shared by both the registered-customer and anonymous-guest
        /// lifecycle scenarios, which differ only in how the order gets placed and paid.
        /// </summary>
        private static async Task<(string OrderStatusAfterPayment, string FinalShipmentStatus)> FulfillThroughDeliveryAsync(
            IPage adminPage,
            string baseAddress,
            int orderId)
        {
            IOrderFulfillmentPage fulfillment = await OrderFulfillmentPage.NavigateAsync(
                adminPage,
                baseAddress,
                orderId);

            var orderStatusAfterPayment = await fulfillment.GetOrderStatusAsync();

            IShipmentCreatePage shipmentCreate = await fulfillment.OpenCreateShipmentAsync();
            IOrderShipmentsPage orderShipments = await shipmentCreate.CreateShipmentAsync();
            IShipmentDetailsPage shipment = await orderShipments.OpenLatestShipmentDetailsAsync();
            shipment = await shipment.DispatchAsync();
            shipment = await shipment.DeliverAsync();

            return (orderStatusAfterPayment, await shipment.GetStatusAsync());
        }

        private static async Task<IOrderSummaryPage> PlaceAnonymousOrderAsync(
            IPage guestPage,
            string baseAddress,
            int productId,
            string email)
        {
            var storefront = await StorefrontPage.NavigateAsync(guestPage, baseAddress);
            var product = await storefront.OpenProductAsync(productId);
            await product.AddToCartAsync(productId, 1);

            var cart = await CartPage.NavigateAsync(guestPage, baseAddress);
            var orderForm = await cart.ProceedToOrderAsync();
            await orderForm.FillGuestCustomerAsync(email);
            var summary = await orderForm.SubmitAsync();
            await summary.ShouldConfirmOrderAsync();
            return summary;
        }
    }
}