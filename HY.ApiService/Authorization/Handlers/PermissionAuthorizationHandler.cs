using HY.ApiService.Authorization.Requirements;
using HY.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HY.ApiService.Authorization.Handlers
{
    /// <summary>
    /// 权限验证处理器
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissionService;

        public PermissionAuthorizationHandler(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var userIdValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!long.TryParse(userIdValue, out var userId)) return;

            var hasPermission = await _permissionService.HasPermissionAsync(userId, requirement.PermissionCode);

            if (hasPermission) context.Succeed(requirement);
        }
    }
}
