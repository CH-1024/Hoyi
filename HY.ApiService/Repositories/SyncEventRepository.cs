using HY.ApiService.Entities;
using SqlSugar;

namespace HY.ApiService.Repositories
{
    public interface ISyncEventRepository
    {
        Task<bool> InsertSyncEventsAsync(List<SyncEventEntity> events);
        Task<long> GetLatestEventIdAsync(long userId);
        Task<List<SyncEventEntity>> GetEventsAfterAsync(long userId, long afterId, int take);
    }

    public class SyncEventRepository : ISyncEventRepository
    {
        private readonly ISqlSugarClient _db;

        public SyncEventRepository(ISqlSugarClient db)
        {
            _db = db;
        }

        public async Task<bool> InsertSyncEventsAsync(List<SyncEventEntity> events)
        {
            if (events.Count == 0)
                return true;

            // Must be called inside the business transaction. The sequence row lock remains
            // held until commit, preserving the per-user event order under concurrent writes.
            foreach (var group in events.GroupBy(e => e.User_Id).OrderBy(g => g.Key))
            {
                var userId = group.Key;
                var count = group.Count();

                await _db.Ado.ExecuteCommandAsync(
                    @"INSERT INTO sync_event_sequence (user_id, last_event_id)
                      VALUES (@userId, 0)
                      ON DUPLICATE KEY UPDATE user_id = VALUES(user_id)",
                    new SugarParameter("@userId", userId));

                var affected = await _db.Updateable<SyncEventSequenceEntity>()
                    .SetColumns(x => x.Last_Event_Id == x.Last_Event_Id + count)
                    .Where(x => x.User_Id == userId)
                    .ExecuteCommandAsync();

                if (affected != 1)
                    throw new Exception($"分配用户 {userId} 的同步事件序号失败");

                var sequence = await _db.Queryable<SyncEventSequenceEntity>()
                    .Where(x => x.User_Id == userId)
                    .FirstAsync();

                if (sequence == null)
                    throw new Exception($"用户 {userId} 的同步事件序列不存在");

                var nextId = sequence.Last_Event_Id - count + 1;
                foreach (var syncEvent in group)
                    syncEvent.User_Event_Id = nextId++;
            }

            var inserted = await _db.Insertable(events).ExecuteCommandAsync();
            return inserted == events.Count;
        }

        public async Task<long> GetLatestEventIdAsync(long userId)
        {
            var sequence = await _db.Queryable<SyncEventSequenceEntity>()
                .Where(x => x.User_Id == userId)
                .FirstAsync();

            return sequence?.Last_Event_Id ?? 0;
        }

        public async Task<List<SyncEventEntity>> GetEventsAfterAsync(long userId, long afterId, int take)
        {
            return await _db.Queryable<SyncEventEntity>()
                .Where(e => e.User_Id == userId && e.User_Event_Id > afterId)
                .OrderBy(e => e.User_Event_Id, OrderByType.Asc)
                .Take(take)
                .ToListAsync();
        }
    }
}
