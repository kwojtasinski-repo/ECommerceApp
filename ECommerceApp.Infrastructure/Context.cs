﻿using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Infrastructure
{
    public class Context : IdentityDbContext
    {
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<ContactDetail> ContactDetails { get; set; }
        public DbSet<ContactDetailType> ContactDetailTypes { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponType> CouponTypes { get; set; }
        public DbSet<CouponUsed> CouponUsed { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ItemTag> ItemTag { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItem { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ECommerceApp.Domain.Model.Type> Types { get; set; }

        public Context(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // -------------------- RELATION N:N --------------------
            builder.Entity<ItemTag>()
                .HasKey(it => new { it.ItemId, it.TagId });

            builder.Entity<ItemTag>()
                .HasOne<Item>(it => it.Item)
                .WithMany(i => i.ItemTags)
                .HasForeignKey(it => it.ItemId);

            builder.Entity<ItemTag>()
                .HasOne<Tag>(it => it.Tag)
                .WithMany(t => t.ItemTags)
                .HasForeignKey(it => it.TagId);
            // -------------------- RELATION N:N --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Coupon>()
                .HasOne(c => c.CouponUsed)
                .WithOne(c => c.Coupon)
                .HasForeignKey<CouponUsed>(c => c.CouponId);
            // -------------------- RELATION 1:1 --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Order>()
                .HasOne(o => o.CouponUsed)
                .WithOne(c => c.Order)
                .HasForeignKey<CouponUsed>(c => c.OrderId);
            // -------------------- RELATION 1:1 --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Refund>()
                .HasOne(r => r.Order)
                .WithOne(o => o.Refund)
                .HasForeignKey<Order>(o => o.RefundId);
            // -------------------- RELATION 1:1 --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Order>(o => o.PaymentId);
            // -------------------- RELATION 1:1 --------------------
        }
    }
}
