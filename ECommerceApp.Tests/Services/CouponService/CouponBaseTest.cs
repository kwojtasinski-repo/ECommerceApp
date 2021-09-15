using AutoMapper;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Services.CouponService
{
    public class CouponBaseTest : BaseTest<Coupon>
    {
        protected readonly ICouponService _service;

        public CouponBaseTest()
        {
            var repo = new Infrastructure.Repositories.CouponRepository(_context);

            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            var mapper = configurationProvider.CreateMapper();
            _service = new Application.Services.CouponService(repo, mapper);
        }
    }
}
