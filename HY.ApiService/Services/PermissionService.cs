using HY.ApiService.Repositories;
using HY.ApiService.Authorization;
using SqlSugar;

namespace HY.ApiService.Services
{
    public interface IPermissionService
    {
        Task<bool> AssignRoleAsync(long userId, string roleCode);

        Task<bool> HasPermissionAsync(long userId, string permissionCode);
        Task<bool> HasRoleAsync(long userId, string roleCode);
    }

    public class PermissionService : IPermissionService
    {
        private readonly ISqlSugarClient _db;
        private readonly IPermissionRepository _permissionRepository;

        public PermissionService(ISqlSugarClient db, IPermissionRepository permissionRepository)
        {
            _db = db;
            _permissionRepository = permissionRepository;
        }


        /// <summary>
        /// 为用户分配角色
        /// </summary>
        public async Task<bool> AssignRoleAsync(long userId, string roleCode)
        {
            var result = await _db.Ado.UseTranAsync(async () =>
            {
                var assigned = await _permissionRepository.AssignRoleByCodeAsync(userId, roleCode);

                if (!assigned) throw new Exception("分配用户角色失败");
            });

            return result.IsSuccess;
        }



        /// <summary>
        /// 判断用户是否具有指定权限
        /// </summary>
        public async Task<bool> HasPermissionAsync(long userId, string permissionCode)
        {
            return await _permissionRepository.HasPermissionAsync(userId, permissionCode);
        }

        /// <summary>
        /// 判断用户是否拥有指定角色
        /// </summary>
        public async Task<bool> HasRoleAsync(long userId, string roleCode)
        {
            return await _permissionRepository.HasRoleAsync(userId, roleCode);
        }
    }
}