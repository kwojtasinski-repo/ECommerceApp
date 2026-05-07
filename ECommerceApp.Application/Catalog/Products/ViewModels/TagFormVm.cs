using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class TagFormVm
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
    }
}
