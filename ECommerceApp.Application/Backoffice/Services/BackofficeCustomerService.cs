using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Sales.Orders;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeCustomerService : IBackofficeCustomerService
    {
        private readonly IUserProfileService _profileService;
        private readonly IOrderService _orderService;

        public BackofficeCustomerService(IUserProfileService profileService, IOrderService orderService)
        {
            _profileService = profileService;
            _orderService = orderService;
        }

        public async Task<BackofficeCustomerListVm> GetCustomersAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var source = await _profileService.GetAllAsync(pageSize, pageNo, searchString ?? string.Empty);
            return new BackofficeCustomerListVm
            {
                Customers = source.Profiles.Select(p => new BackofficeCustomerItemVm
                {
                    Id = p.Id,
                    FullName = $"{p.FirstName} {p.LastName}",
                    UserId = p.UserId,
                    IsCompany = p.IsCompany
                }).ToList(),
                CurrentPage = source.CurrentPage,
                PageSize = source.PageSize,
                TotalCount = source.Count,
                SearchString = searchString
            };
        }

        public async Task<BackofficeCustomerDetailVm> GetCustomerDetailAsync(int customerId, CancellationToken ct = default)
        {
            var detail = await _profileService.GetDetailsAsync(customerId);
            if (detail is null)
                return null;

            return new BackofficeCustomerDetailVm
            {
                Id = detail.Id,
                FirstName = detail.FirstName,
                LastName = detail.LastName,
                UserId = detail.UserId,
                IsCompany = detail.IsCompany
            };
        }

        public async Task<BackofficeOrderListVm> GetOrdersByCustomerAsync(int customerId, int pageSize, int pageNo, CancellationToken ct = default)
        {
            var all = await _orderService.GetOrdersByCustomerIdAsync(customerId, ct);
            var items = all
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new BackofficeOrderItemVm
                {
                    Id = o.Id,
                    Number = o.Number,
                    Cost = o.Cost,
                    Status = o.Status.ToString(),
                    IsPaid = IsPaidStatus(o.Status)
                })
                .ToList();

            return new BackofficeOrderListVm
            {
                Orders = items,
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = all.Count
            };
        }

        private static bool IsPaidStatus(OrderStatus status)
            => status is OrderStatus.PaymentConfirmed
                or OrderStatus.PartiallyFulfilled
                or OrderStatus.Fulfilled
                or OrderStatus.Refunded;
    }
}

