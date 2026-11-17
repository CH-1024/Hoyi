using HY.ApiService.Entities;
using SqlSugar;

namespace HY.ApiService.Repositories
{
    public interface ISyncEventRepository
    {
        Task<bool> InsertSyncEventAsync(SyncEventEntity syncEvent);
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

        public async Task<bool> InsertSyncEventAsync(SyncEventEntity syncEvent)
        {
            syncEvent.Id = await _db.Insertable(syncEvent).ExecuteReturnBigIdentityAsync();
            return syncEvent.Id > 0;
        }

        public async Task<bool> InsertSyncEventsAsync(List<SyncEventEntity> events)
        {
            if (events.Count == 0) return true;

            var ids = await _db.Insertable(events).ExecuteReturnPkListAsync<long>();

            if (ids.Count != events.Count) return false;

            // 按插入顺序回填 ID
            for (int i = 0; i < events.Count; i++)
            {
                events[i].Id = ids[i];
            }

            return true;
        }



        public async Task<long> GetLatestEventIdAsync(long userId)
        {
            var latest = await _db.Queryable<SyncEventEntity>()
                .Where(e => e.User_Id == userId)
                .OrderBy(e => e.Id, OrderByType.Desc)
                .FirstAsync();

            return latest?.Id ?? 0;
        }

        public async Task<List<SyncEventEntity>> GetEventsAfterAsync(long userId, long afterId, int take)
        {
            return await _db.Queryable<SyncEventEntity>()
                .Where(e => e.User_Id == userId && e.Id > afterId)
                .OrderBy(e => e.Id, OrderByType.Asc)
                .Take(take)
                .ToListAsync();
        }
    }
}
