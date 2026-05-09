namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class UserProfileForListVm
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool IsCompany { get; set; }
        public string CompanyName { get; set; }
        public string Email { get; set; } = default!;

        public static UserProfileForListVm FromDomain(global::ECommerceApp.Domain.AccountProfile.UserProfile s) => new()
        {
            Id = s.Id != null ? s.Id.Value : 0,
            UserId = s.UserId,
            FirstName = s.FirstName,
            LastName = s.LastName,
            IsCompany = s.IsCompany,
            CompanyName = s.CompanyName?.Value,
            Email = s.Email.Value
        };
    }
}
