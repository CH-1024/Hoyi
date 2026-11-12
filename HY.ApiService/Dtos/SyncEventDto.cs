namespace HY.ApiService.Dtos
{
    public class SyncEventDto
    {
        public long Id { get; set; }
        public string Event_Type { get; set; } = string.Empty;
        public long Entity_Id { get; set; }
        public long Related_Id { get; set; }
        public DateTime Created_At { get; set; }
    }
}
