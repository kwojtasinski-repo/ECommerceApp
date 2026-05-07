using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class CategoryFormVm
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }
}
