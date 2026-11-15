using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Repositories;
using Mapster;
using SqlSugar;

namespace HY.ApiService.Services
{
    public interface ISyncEventService
    {
        Task<bool> InsertSyncEventsAsync(long userId, List<SyncEventDto> events);
        Task<long> GetLatestEventIdAsync(long userId);
        Task<List<SyncEventDto>> GetEventsAfterAsync(long userId, long afterId, int take);
    }


    public class SyncEventService : ISyncEventService
    {
        private readonly ISqlSugarClient _db;

        private readonly ISyncEventRepository _syncEventRepository;

        public SyncEventService(ISqlSugarClient db, ISyncEventRepository syncEventRepository)
        {
            _db = db;

            _syncEventRepository = syncEventRepository;
        }


        public async Task<bool> InsertSyncEventsAsync(long userId, List<SyncEventDto> events)
        {
            var entities = events.Adapt<List<SyncEventEntity>>();

            entities.ForEach(e => e.User_Id = userId);

            return await _syncEventRepository.InsertSyncEventsAsync(entities);
        }

        public async Task<long> GetLatestEventIdAsync(long userId)
        {
            return await _syncEventRepository.GetLatestEventIdAsync(userId);
        }

        public async Task<List<SyncEventDto>> GetEventsAfterAsync(long userId, long afterId, int take)
        {
            var entities = await _syncEventRepository.GetEventsAfterAsync(userId, afterId, take);

            return entities.Adapt<List<SyncEventDto>>();
        }

    }
}
