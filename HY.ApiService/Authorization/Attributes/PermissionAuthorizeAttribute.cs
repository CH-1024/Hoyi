using Microsoft.AspNetCore.Authorization;

namespace HY.ApiService.Authorization.Attributes
{
    /// <summary>
    /// 接口权限特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "Permission:";

        public PermissionAuthorizeAttribute(string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
                throw new ArgumentException("权限编码不能为空", nameof(permissionCode));

            Policy = PolicyPrefix + permissionCode;
        }
    }
}