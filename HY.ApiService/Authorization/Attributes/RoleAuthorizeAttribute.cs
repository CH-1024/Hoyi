using Microsoft.AspNetCore.Authorization;

namespace HY.ApiService.Authorization.Attributes
{
    /// <summary>
    /// 角色验证特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "Role:";

        public RoleAuthorizeAttribute(string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
                throw new ArgumentException("角色编码不能为空", nameof(roleCode));

            Policy = PolicyPrefix + roleCode;
        }
    }
}