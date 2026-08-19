using ECommerceApp.Domain.AccountProfile;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class UserProfileTestData
    {
        public static UserProfile Create(
            string userId,
            string email = null,
            string phoneNumber = "500600700")
        {
            return UserProfile.Create(
                userId,
                "Jan",
                "Kowalski",
                false,
                null,
                null,
                email ?? $"{userId}@example.com",
                phoneNumber);
        }
    }
}