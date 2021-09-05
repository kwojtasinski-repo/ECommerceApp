using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Image
{
    public class GetImageVm : BaseVm
    {
        public string Name { get; set; }
        public int? ItemId { get; set; }
        public byte[] ImageSource { get; set; }
    }
}
