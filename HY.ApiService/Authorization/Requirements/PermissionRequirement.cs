using Microsoft.AspNetCore.Authorization;

namespace HY.ApiService.Authorization.Requirements
{
    /// <summary>
    /// 权限要求
    /// </summary>
    public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
}
