using ECommerceApp.Application.ViewModels;
using System;

namespace ECommerceApp.Application.Interfaces
{
    public interface IErrorMapToResponse
    {
        ExceptionResponse Map(Exception exception);
    }
}
