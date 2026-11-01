using HY.ApiService.Authorization.Attributes;
using HY.ApiService.Authorization.Requirements;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HY.ApiService.Authorization
{
    /// <summary>
    /// 动态权限策略提供器
    /// </summary>
    public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
        {
        }

        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // 权限策略
            if (policyName.StartsWith(PermissionAuthorizeAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var code = policyName[PermissionAuthorizeAttribute.PolicyPrefix.Length..];

                if (string.IsNullOrWhiteSpace(code))
                    return Task.FromResult<AuthorizationPolicy?>(null);

                return Task.FromResult<AuthorizationPolicy?>(CreatePolicy(new PermissionRequirement(code)));
            }

            // 角色策略
            if (policyName.StartsWith(RoleAuthorizeAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var code = policyName[RoleAuthorizeAttribute.PolicyPrefix.Length..];

                if (string.IsNullOrWhiteSpace(code))
                    return Task.FromResult<AuthorizationPolicy?>(null);

                return Task.FromResult<AuthorizationPolicy?>(CreatePolicy(new RoleRequirement(code)));
            }

            // 其他策略交给默认提供器
            return base.GetPolicyAsync(policyName);
        }

        /// <summary>
        /// 创建统一授权策略
        /// </summary>
        private static AuthorizationPolicy CreatePolicy(IAuthorizationRequirement requirement)
        {
            return new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(requirement)
                .Build();
        }
    }
}
