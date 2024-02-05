namespace ECommerceApp.Domain.Model
{
    public class ContactDetail : BaseEntity
    {
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public ContactDetailType ContactDetailType { get; set; }
    }
}
