using SqlSugar;

namespace HY.ApiService.Entities
{
    [SugarTable("sync_event_sequence")]
    public class SyncEventSequenceEntity
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long User_Id { get; set; }

        public long Last_Event_Id { get; set; }
    }
}
