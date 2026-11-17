using HY.ApiService.Entities;
using HY.ApiService.Enums;

namespace HY.ApiService.Tools
{
    public static class SyncEventFactory
    {
        //                                      entityId                relatedId

        //MessageCreated,                       messageId               chatId
        //MessageRecalled,                      messageId               chatId
        //MessageDeleted,                       messageId               chatId
        //ChatChanged,                          chatId
        //ChatRead,                             chatId
        //ContactsChanged,                      userId
        //ContactRequestsChanged,               contactRequestId
        //ProfileChanged                        userId 

        public static SyncEventEntity Create(long userId, SyncEventTypes eventType, long entityId = 0, long relatedId = 0)
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
