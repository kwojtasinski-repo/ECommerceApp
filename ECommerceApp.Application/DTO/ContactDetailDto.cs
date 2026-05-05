using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class ContactDetailDto
    {
        public int Id { get; set; }
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public int CustomerId { get; set; }

        public class ContactDetailDtoValidation : AbstractValidator<ContactDetailDto>
        {
            public ContactDetailDtoValidation()
            {
                RuleFor(x => x.Id).NotNull();
                When(x => x.ContactDetailTypeId == 1, () =>
                {
                    RuleFor(x => x.ContactDetailInformation).Length(9);
                });
                When(x => x.ContactDetailTypeId == 2, () =>
                {
                    RuleFor(x => x.ContactDetailInformation).EmailAddress();
                });
                RuleFor(x => x.ContactDetailInformation).NotNull().MaximumLength(100);
                RuleFor(x => x.ContactDetailTypeId).NotNull();
                RuleFor(x => x.CustomerId).NotNull();
            }
        }
    }
}
