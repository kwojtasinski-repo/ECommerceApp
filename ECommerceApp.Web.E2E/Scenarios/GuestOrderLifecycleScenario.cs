using ECommerceApp.Web.E2E.PageObjects;
using Microsoft.Playwright;
using Shouldly;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Scenarios
{
    public sealed class GuestOrderLifecycleScenario
    {
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

        public async Task<int> ExecuteAnonymousCookieRecoveryAsync(
            IPage guestPage,
            IPage adminPage,
            string baseAddress,
            int productId)
        {
            var email = $"recovery-{Guid.NewGuid():N}@example.com";
            var summary = await PlaceAnonymousOrderAsync(guestPage, baseAddress, productId, email);
            var orderId = await summary.GetOrderIdAsync();
            var accessToken = GetQueryParameter(new Uri(guestPage.Url), "token");
            accessToken.ShouldNotBeNullOrWhiteSpace();

            await guestPage.Context.ClearCookiesAsync();
            await guestPage.GotoAsync($"{baseAddress}/Identity/Account/Login?guestOrder={Uri.EscapeDataString(accessToken)}");
            await guestPage.Locator("#guest-order-recovery input[type='email']").FillAsync(email);
            await guestPage.GetByRole(AriaRole.Button, new() { Name = "Przygotuj odzyskanie dostępu" }).ClickAsync();
            await guestPage.WaitForURLAsync("**/Identity/Account/Login?guestOrder=*");

            await adminPage.GotoAsync($"{baseAddress}/Backoffice/GuestVerification");
            var redemptionLink = await adminPage.Locator("a[href*='RedeemRecovery']").First.GetAttributeAsync("href");
            redemptionLink.ShouldNotBeNullOrWhiteSpace();

            await guestPage.GotoAsync(redemptionLink);
            await guestPage.WaitForURLAsync("**/Presale/Checkout/Summary/*");
            (await guestPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(1);

            return orderId;
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

            IOrderFulfillmentPage fulfillment = await OrderFulfillmentPage.NavigateAsync(
                adminPage,
                baseAddress,
                orderId);

            // Read from the admin's page rather than assuming: this is what proves the customer's
            // payment actually reached the read model the back office works from.
            var orderStatusAfterPayment = await fulfillment.GetOrderStatusAsync();

            IShipmentCreatePage shipmentCreate = await fulfillment.OpenCreateShipmentAsync();
            IOrderShipmentsPage orderShipments = await shipmentCreate.CreateShipmentAsync();
            IShipmentDetailsPage shipment = await orderShipments.OpenLatestShipmentDetailsAsync();
            shipment = await shipment.DispatchAsync();
            shipment = await shipment.DeliverAsync();

            return new OrderLifecycleResult(
                orderId,
                orderStatusAfterPayment == "PaymentConfirmed",
                await shipment.GetStatusAsync());
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

        private static string GetQueryParameter(Uri uri, string key)
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.Ordinal))
                    return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }

            return null;
        }
    }
}