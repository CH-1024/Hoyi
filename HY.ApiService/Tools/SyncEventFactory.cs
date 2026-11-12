using HY.ApiService.Entities;

namespace HY.ApiService.Tools
{
    public static class SyncEventFactory
    {
        public static SyncEventEntity Create(
            long userId,
            string eventType,
            long entityId = 0,
            long relatedId = 0)
        {
            return new SyncEventEntity
            {
                User_Id = userId,
                Event_Type = eventType,
                Entity_Id = entityId,
                Related_Id = relatedId,
                Created_At = DateTime.UtcNow
            };
        }
    }
}
