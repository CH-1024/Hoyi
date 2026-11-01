using HY.ApiService.Entities;
using SqlSugar;

namespace HY.ApiService.Repositories
{
    public interface IPermissionRepository
    {
        Task<bool> AssignRoleByCodeAsync(long userId, string roleCode);

        Task<bool> HasPermissionAsync(long userId, string permissionCode);
        Task<bool> HasRoleAsync(long userId, string roleCode);
    }

    public class PermissionRepository : IPermissionRepository
    {
        private readonly ISqlSugarClient _db;

        public PermissionRepository(ISqlSugarClient db)
        {
            _db = db;
        }


        /// <summary>
        /// 根据角色编码分配角色
        /// </summary>
        public async Task<bool> AssignRoleByCodeAsync(long userId, string roleCode)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(roleCode)) return false;

            // 查询有效角色
            var roleIds = await _db.Queryable<SysRoleEntity>()
                .Where(x => x.Code == roleCode && x.Status == 1)
                .Select(x => x.Id)
                .ToListAsync();

            if (roleIds.Count == 0) return false;

            var roleId = roleIds[0];

            // 判断是否已经分配
            var exists = await _db.Queryable<SysUserRoleEntity>()
                .AnyAsync(x => x.User_Id == userId && x.Role_Id == roleId);

            if (exists) return true;

            // 创建用户角色关联
            var entity = new SysUserRoleEntity
            {
                User_Id = userId,
                Role_Id = roleId,
                Created_At = DateTime.UtcNow
            };

            var result = await _db.Insertable(entity).ExecuteCommandAsync();

            return result > 0;
        }



        /// <summary>
        /// 判断用户是否拥有指定权限
        /// </summary>
        public async Task<bool> HasPermissionAsync(long userId, string permissionCode)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(permissionCode)) return false;

            // 查询有效权限
            var permissionIds = await _db.Queryable<SysPermissionEntity>()
                .Where(x => x.Code == permissionCode && x.Status == 1)
                .Select(x => x.Id)
                .ToListAsync();

            if (permissionIds.Count == 0) return false;

            // 查询用户关联的角色
            var roleIds = await _db.Queryable<SysUserRoleEntity>()
                .Where(x => x.User_Id == userId)
                .Select(x => x.Role_Id)
                .ToListAsync();

            if (roleIds.Count == 0) return false;

            // 筛选有效角色
            var activeRoleIds = await _db.Queryable<SysRoleEntity>()
                .Where(x => roleIds.Contains(x.Id) && x.Status == 1)
                .Select(x => x.Id)
                .ToListAsync();

            if (activeRoleIds.Count == 0) return false;

            // 检查角色是否拥有指定权限
            return await _db.Queryable<SysRolePermissionEntity>()
                .AnyAsync(x => activeRoleIds.Contains(x.Role_Id) && permissionIds.Contains(x.Permission_Id));
        }

        /// <summary>
        /// 判断用户是否拥有指定角色
        /// </summary>
        public async Task<bool> HasRoleAsync(long userId, string roleCode)
        {
            return await _db.Queryable<SysUserRoleEntity>().InnerJoin<SysRoleEntity>((ur, r) => ur.Role_Id == r.Id)
                .Where((ur, r) => ur.User_Id == userId && r.Code == roleCode && r.Status == 1)
                .AnyAsync();
        }
    }
}