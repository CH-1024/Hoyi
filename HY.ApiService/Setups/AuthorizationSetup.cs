using HY.ApiService.Authorization;
using HY.ApiService.Authorization.Handlers;
using HY.ApiService.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace HY.ApiService.Setups
{
    public static class AuthorizationSetup
    {
        /// <summary>
        /// 添加授权配置
        /// </summary>
        public static void AddAuthorizationSetup(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // 系统管理员
                options.AddPolicy("AdminOnly", policy =>
                {
                    policy.AddRequirements(new PermissionRequirement(PermissionCodes.SystemAdmin));
                });

                // 其他原有策略可以继续保留
            });

            // 动态策略提供器
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

            // 权限验证处理器
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

            // 角色验证处理器
            services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
        }
    }

}
