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

            var affected = await _db.Insertable(events).ExecuteCommandAsync();
            return affected == events.Count;
        }

        public async Task<long> GetLatestEventIdAsync(long userId)
        {
            var latest = await _db.Queryable<SyncEventEntity>()
                .Where(e => e.User_Id == userId)
                .OrderBy(e => e.Id, OrderByType.Desc)
                .FirstAsync();

            return latest?.Id ?? 0;
        }

        public async Task<List<SyncEventEntity>> GetEventsAfterAsync(
            long userId,
            long afterId,
            int take)
        {
            return await _db.Queryable<SyncEventEntity>()
                .Where(e => e.User_Id == userId && e.Id > afterId)
                .OrderBy(e => e.Id, OrderByType.Asc)
                .Take(take)
                .ToListAsync();
        }
    }
}
