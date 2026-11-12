using SqlSugar;

namespace HY.ApiService.Entities
{
    [SugarTable("sync_event")]
    public class SyncEventEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        public long User_Id { get; set; }

        [SugarColumn(Length = 48, IsNullable = false)]
        public string Event_Type { get; set; } = string.Empty;

        public long Entity_Id { get; set; }

        public long Related_Id { get; set; }

        public DateTime Created_At { get; set; }
    }
}
