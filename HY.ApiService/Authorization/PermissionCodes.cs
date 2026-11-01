namespace HY.ApiService.Authorization
{
    public static class PermissionCodes
    {
        public const string SystemAdmin = "system.admin";

        public static class Chat
        {
            public const string Delete = "chat.delete";
            public const string Manage = "chat.manage";
        }

        public static class File
        {
            public const string Upload = "file.upload";
            public const string Manage = "file.manage";
        }
    }

    public static class RoleCodes
    {
        public const string Guest = "guest";
        public const string User = "user";
        public const string Admin = "admin";
        public const string SuperAdmin = "super_admin";
    }
}