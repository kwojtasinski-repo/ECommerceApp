using FluentValidation;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceApp.Application.DTO
{
    public class AddOrderFromCartDto
    {
        [Column(TypeName = "decimal(18,2)")]
        public string PromoCode { get; set; }
        public int CustomerId { get; set; }
    }

    public class AddOrderFromCartDtoValidation : AbstractValidator<AddOrderFromCartDto>
    {
        public AddOrderFromCartDtoValidation()
        {
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
