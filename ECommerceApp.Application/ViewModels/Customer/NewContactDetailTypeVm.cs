using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class NewContactDetailTypeVm : IMapFrom<ECommerceApp.Domain.Model.ContactDetailType>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewContactDetailTypeVm, ECommerceApp.Domain.Model.ContactDetailType>().ReverseMap();
        }
    }
}
