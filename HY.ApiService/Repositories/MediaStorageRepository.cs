using HY.ApiService.Entities;
using SqlSugar;

namespace HY.ApiService.Repositories
{
    public interface IMediaStorageRepository
    {
        Task<long> CreateMediaStorage(MediaStorageEntity mediaStorage);

        Task<MediaStorageEntity?> GetMediaStorageById(long Id);
        Task<MediaStorageEntity?> GetMediaStorageByFileSHA256(string sha256);

        Task<bool> UpdateMediaStorage(MediaStorageEntity mediaStorageEntity);
        Task<bool> UpdateMediaStorageRefCount(long id, int ref_Count);
    }


    public class MediaStorageRepository : IMediaStorageRepository
    {
        private readonly ISqlSugarClient _db;

        public MediaStorageRepository(ISqlSugarClient db)
        {
            _db = db;
        }


        public async Task<long> CreateMediaStorage(MediaStorageEntity mediaStorage)
        {
            mediaStorage.Id = await _db.Insertable(mediaStorage).ExecuteReturnBigIdentityAsync();
            return mediaStorage.Id;
        }





        public async Task<MediaStorageEntity?> GetMediaStorageById(long Id)
        {
            return await _db.Queryable<MediaStorageEntity>().Where(ms => ms.Id == Id && ms.Status == 1).SingleAsync();
        }

        public async Task<MediaStorageEntity?> GetMediaStorageByFileSHA256(string sha256)
        {
            return await _db.Queryable<MediaStorageEntity>().Where(ms => ms.File_SHA256 == sha256).SingleAsync();
        }




        public async Task<bool> UpdateMediaStorage(MediaStorageEntity mediaStorageEntity)
        {
            var result = await _db.Updateable(mediaStorageEntity).ExecuteCommandAsync();
            return result > 0;
        }

        public async Task<bool> UpdateMediaStorageRefCount(long id, int ref_Count)
        {
            var result = await _db.Updateable<MediaStorageEntity>()
                .SetColumns(ms => ms.Ref_Count == ref_Count)
                .Where(ms => ms.Id == id)
                .ExecuteCommandAsync();
            return result > 0;
        }

    }
}
