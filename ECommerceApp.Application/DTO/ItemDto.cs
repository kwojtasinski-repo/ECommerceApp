using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.DTO
{
    public class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public TypeDto Type { get; set; }
        public CurrencyDto Currency { get; set; }
    }
}
