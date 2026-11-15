using HY.ApiService.Entities;
using HY.ApiService.Enums;
using SqlSugar;
using System.Collections;
using System.Net.NetworkInformation;

namespace HY.ApiService.Repositories
{
    public interface IMessageRepository
    {
        Task<long> InsertMessage(MessageEntity messageEntity);

        Task<MessageEntity?> GetMessageByIdWithoutFilter(long messageId);
        Task<MessageEntity?> GetMessageById(long currentUserId, long messageId);
        Task<List<MessageEntity>> GetMessagesByIds(long currentUserId, List<long> msgIds);
        Task<List<MessageEntity>> GetPrivateChatMessages(long currentUserId, long targetUserId, DirectType order, long messageId, int take);
        Task<List<MessageEntity>> GetGroupChatMessages(long currentUserId, long groupId, DirectType order, long messageId, int take);

        Task<bool> RecallMessage(long messageId);
    }


    public class MessageRepository : IMessageRepository
    {
        private readonly ISqlSugarClient _db;

        public MessageRepository(ISqlSugarClient db)
        {
            _db = db;
        }




        public async Task<long> InsertMessage(MessageEntity messageEntity)
        {
            messageEntity.Id = await _db.Insertable(messageEntity).ExecuteReturnBigIdentityAsync();
            return messageEntity.Id;
        }


        public async Task<MessageEntity?> GetMessageByIdWithoutFilter(long messageId)
        {
            return await _db.Queryable<MessageEntity>().InSingleAsync(messageId);
        }

        public async Task<MessageEntity?> GetMessageById(long currentUserId, long messageId)
        {
            return await _db.Queryable<MessageEntity>()
                .LeftJoin<MessageActionEntity>((m, a) => m.Id == a.Message_Id && a.User_Id == currentUserId && a.Action_Type == MessageActionType.Delete)
                .Where((m, a) => a.Message_Id == null)   // 过滤已删除
                .InSingleAsync(messageId);
        }

        public async Task<List<MessageEntity>> GetMessagesByIds(long currentUserId, List<long> msgIds)
        {
            return await _db.Queryable<MessageEntity>()
                .LeftJoin<MessageActionEntity>((m, a) => m.Id == a.Message_Id && a.User_Id == currentUserId && a.Action_Type == MessageActionType.Delete)
                .Where((m, a) => a.Message_Id == null)   // 过滤已删除
                .In(msgIds).ToListAsync();
        }

        public async Task<List<MessageEntity>> GetPrivateChatMessages(long currentUserId, long targetUserId, DirectType direct, long messageId, int take)
        {
            var baseQuery = _db.Queryable<MessageEntity>()
                .LeftJoin<MessageActionEntity>((m, a) => m.Id == a.Message_Id && a.User_Id == currentUserId && a.Action_Type == MessageActionType.Delete)
                .Where((m, a) => a.Message_Id == null)   // 过滤已删除
                .Where((m, a) => m.Chat_Type == ChatType.Private)
                .WhereIF(direct == DirectType.Before, (m, a) => m.Id < messageId)
                .WhereIF(direct == DirectType.After, (m, a) => m.Id > messageId);

            var q1 = baseQuery.Clone().Where((m, a) => m.Sender_Id == currentUserId && m.Target_Id == targetUserId).Select((m, a) => m);
            var q2 = baseQuery.Clone().Where((m, a) => m.Sender_Id == targetUserId && m.Target_Id == currentUserId).Select((m, a) => m);

            // UNION ALL 合并
            return await _db.UnionAll(q1, q2).OrderBy(m => m.Id, OrderByType.Desc).Take(take).ToListAsync();
        }

        public async Task<List<MessageEntity>> GetGroupChatMessages(long currentUserId, long groupId, DirectType direct, long messageId, int take)
        {
            return await _db.Queryable<MessageEntity>()
                .LeftJoin<MessageActionEntity>((m, a) => m.Id == a.Message_Id && a.User_Id == currentUserId && a.Action_Type == MessageActionType.Delete)
                .Where((m, a) => a.Message_Id == null)   // 过滤已删除
                .Where((m, a) => m.Target_Id == groupId && m.Chat_Type == ChatType.Group)
                .WhereIF(direct == DirectType.Before, (m, a) => m.Id < messageId)
                .WhereIF(direct == DirectType.After, (m, a) => m.Id > messageId)
                .OrderBy((m, a) => m.Id, OrderByType.Desc)
                .Take(take)
                .ToListAsync();
        }



        public async Task<bool> RecallMessage(long messageId)
        {
            var result = await _db.Updateable<MessageEntity>()
                .SetColumns(m => m.Message_Status == MessageStatus.Recalled)
                .SetColumns(m => m.Content == null)
                .SetColumns(m => m.Extra == null)
                .Where(m => m.Id == messageId)
                .ExecuteCommandAsync();
            return result > 0;
        }
    }
}
