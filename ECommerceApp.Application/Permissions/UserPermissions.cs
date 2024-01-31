namespace ECommerceApp.Application.Permissions
{
    public static class UserPermissions
    {
        public static class Roles
        {
            public const string Administrator = "Administrator";
            public const string Manager = "Manager";
            public const string Service = "Service";
            public const string User = "User";

            public static string[] ManagingRoles = new string[] { Administrator, Manager };
            public static string[] MaintenanceRoles = new string[] { Administrator, Manager, Service };
        }
    }
}
