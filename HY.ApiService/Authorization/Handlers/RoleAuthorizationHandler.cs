using HY.ApiService.Authorization.Requirements;
using HY.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HY.ApiService.Authorization.Handlers
{
    /// <summary>
    /// 角色验证处理器
    /// </summary>
    public sealed class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
    {
        private readonly IPermissionService _permissionService;

        public RoleAuthorizationHandler(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
        {
            var userIdValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!long.TryParse(userIdValue, out var userId)) return;

            var hasRole = await _permissionService.HasRoleAsync(userId, requirement.RoleCode);

            if (hasRole) context.Succeed(requirement);
        }
    }
}
