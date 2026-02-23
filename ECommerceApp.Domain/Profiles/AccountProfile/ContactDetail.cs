using System;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public class ContactDetail
    {
        public int Id { get; private set; }
        public string Information { get; private set; } = default!;
        public int ContactDetailTypeId { get; private set; }
        public int AccountProfileId { get; private set; }

        private ContactDetail() { }

        public static ContactDetail Create(int accountProfileId, int contactDetailTypeId, string information)
        {
            if (accountProfileId <= 0)
                throw new ArgumentException("AccountProfileId must be positive", nameof(accountProfileId));
            if (contactDetailTypeId <= 0)
                throw new ArgumentException("ContactDetailTypeId must be positive", nameof(contactDetailTypeId));
            if (string.IsNullOrWhiteSpace(information))
                throw new ArgumentException("Information cannot be empty", nameof(information));

            return new ContactDetail
            {
                AccountProfileId = accountProfileId,
                ContactDetailTypeId = contactDetailTypeId,
                Information = information
            };
        }

        public void Update(int contactDetailTypeId, string information)
        {
            if (contactDetailTypeId <= 0)
                throw new ArgumentException("ContactDetailTypeId must be positive", nameof(contactDetailTypeId));
            if (string.IsNullOrWhiteSpace(information))
                throw new ArgumentException("Information cannot be empty", nameof(information));

            ContactDetailTypeId = contactDetailTypeId;
            Information = information;
        }
    }
}
