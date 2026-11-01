using SqlSugar;

namespace HY.ApiService.Entities
{
    [SugarTable("sys_user_role")]
    public class SysUserRoleEntity
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long User_Id { get; set; }

        [SugarColumn(IsPrimaryKey = true)]
        public long Role_Id { get; set; }

        public DateTime Created_At { get; set; }
    }
}
