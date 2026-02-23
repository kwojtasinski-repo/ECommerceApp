using System;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public class ContactDetailType
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = default!;

        private ContactDetailType() { }

        public static ContactDetailType Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));
            return new ContactDetailType { Name = name };
        }

        public void UpdateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));
            Name = name;
        }
    }
}
