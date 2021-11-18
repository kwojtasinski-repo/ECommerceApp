using ECommerceApp.Application.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IErrorMapToResponse
    {
        ExceptionResponse Map(Exception exception);
    }
}
