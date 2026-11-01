using Microsoft.AspNetCore.Authorization;

namespace HY.ApiService.Authorization.Requirements
{
    /// <summary>
    /// 角色要求
    /// </summary>
    public sealed record RoleRequirement(string RoleCode) : IAuthorizationRequirement;
}
