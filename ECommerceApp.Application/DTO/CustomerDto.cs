using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class CustomerDto
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }
    }


    public class CustomerDtoValidator : AbstractValidator<CustomerDto>
    {
        public CustomerDtoValidator()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.FirstName).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.LastName).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.IsCompany).NotNull();
            When(x => x.IsCompany, () =>
            {
                RuleFor(x => x.NIP).NotNull().Length(9);
                RuleFor(x => x.CompanyName).NotNull().MinimumLength(3).MaximumLength(255);
            });
        }
    }
}
