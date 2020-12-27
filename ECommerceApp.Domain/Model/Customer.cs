﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace ECommerceApp.Domain.Model
{
    public class Customer
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public IdentityUser User { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public virtual ICollection<ContactDetail> ContactDetails { get; set; }
        public virtual ICollection<Address> Addresses { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
        public ICollection<Payment> Payments { get; set; }
        public ICollection<Refund> Refunds { get; set; }  
    }
}
