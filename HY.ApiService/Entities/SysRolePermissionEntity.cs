using SqlSugar;

namespace HY.ApiService.Entities
{
    [SugarTable("sys_role_permission")]
    public class SysRolePermissionEntity
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long Role_Id { get; set; }

        [SugarColumn(IsPrimaryKey = true)]
        public long Permission_Id { get; set; }

        public DateTime Created_At { get; set; }
    }
}
