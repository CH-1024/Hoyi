using SqlSugar;

namespace HY.ApiService.Entities
{
    [SugarTable("sys_permission")]
    public class SysPermissionEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Module { get; set; }
        public string? Description { get; set; }
        public int Status { get; set; } = 1;
        public DateTime Created_At { get; set; }
    }
}
