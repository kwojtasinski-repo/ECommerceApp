using ECommerceApp.Web.E2E.PageObjects;
using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Scenarios
{
    public sealed class GuestOrderLifecycleScenario
    {
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
            IShipmentCreatePage shipmentCreate = await fulfillment.OpenCreateShipmentAsync();
            IOrderShipmentsPage orderShipments = await shipmentCreate.CreateShipmentAsync();
            IShipmentDetailsPage shipment = await orderShipments.OpenLatestShipmentDetailsAsync();
            shipment = await shipment.DispatchAsync();
            shipment = await shipment.DeliverAsync();

            return new OrderLifecycleResult(orderId, true, await shipment.GetStatusAsync());
        }
    }
}