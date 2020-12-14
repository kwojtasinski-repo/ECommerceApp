using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class NewCustomerForOrdersVm : IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public int Id { get; set; }
        public string Information { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewCustomerForOrdersVm, ECommerceApp.Domain.Model.Customer>().ReverseMap()
                .ForMember(i => i.Information, opt => opt.MapFrom(c => (c.NIP != null && c.CompanyName != null) ?
                            c.FirstName + " " + c.LastName + " " + c.NIP + " " + c.CompanyName
                            : c.FirstName + " " + c.LastName));
                
                
                
                
        }
    }
}
