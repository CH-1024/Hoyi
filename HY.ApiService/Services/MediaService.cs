using Azure.Core;
using Dm;
using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
using HY.ApiService.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client.Extensions.Msal;
using OracleInternal.Secure.Network;
using SkiaSharp;
using SqlSugar;
using System.Configuration;
using System.Net.Sockets;
using System.Security.Cryptography;
using SHA256 = System.Security.Cryptography.SHA256;
using StorageType = HY.ApiService.Enums.StorageType;

namespace HY.ApiService.Services
{
    public record CreateMediaFileReturn(bool IsSucc, string? Error = null, string? FileId = null);
    public record UploadMediaFileReturn(bool IsSucc, string? Error = null, string? FileId = null);
    public record GetMediaStorageReturn(bool IsSucc, string? Error = null, bool? IsExist = null, long? StorageId = null, StorageType? StorageType = null, string? Path = null, string? ContentType = null, string? FileDownloadName = null);

    public interface IMediaService
    {
        Task<CreateMediaFileReturn> CreateMediaFile(long userId, long storageId, string fileName, FileType fileType);

        Task<UploadMediaFileReturn> UploadImageToLocal(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadImageToCOS(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadImageToOSS(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadImageToS3(long userId, IFormFile file);

        Task<UploadMediaFileReturn> UploadVideoToLocal(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadVideoToCOS(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadVideoToOSS(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadVideoToS3(long userId, IFormFile file);

        Task<UploadMediaFileReturn> UploadHeadToLocal(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadHeadToCOS(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadHeadToOSS(long userId, IFormFile file);
        Task<UploadMediaFileReturn> UploadHeadToS3(long userId, IFormFile file);

        Task<GetMediaStorageReturn> GetMediaStorageByFileId(string fileId);
        Task<GetMediaStorageReturn> GetMediaStorageVariantByFileId(string fileId, VariantType variantType);
        Task<GetMediaStorageReturn> GetMediaStorageByFileSHA256(string fileSHA, long size);
    }


    public class MediaService : IMediaService
    {
        private readonly ISqlSugarClient _db;

        private readonly IConfiguration _configuration;
        private readonly IMediaStorageRepository _mediaStorageRepository;
        private readonly IMediaFileRepository _mediaFileRepository;
        private readonly IMediaStorageVariantRepository _mediaStorageVariantRepository;

        public MediaService(ISqlSugarClient db, IConfiguration configuration, IMediaStorageRepository mediaStorageRepository, IMediaFileRepository mediaFileRepository, IMediaStorageVariantRepository mediaStorageVariantRepository)
        {
            _db = db;

            _configuration = configuration;
            _mediaStorageRepository = mediaStorageRepository;
            _mediaFileRepository = mediaFileRepository;
            _mediaStorageVariantRepository = mediaStorageVariantRepository;
        }



        public async Task<CreateMediaFileReturn> CreateMediaFile(long userId, long storageId, string fileName, FileType fileType)
        {
            var mediaStorageEntity = await _mediaStorageRepository.GetMediaStorageById(storageId);
            if (mediaStorageEntity != null && mediaStorageEntity.Status == 1)
            {
                // 文件已存在且可用，增加引用计数

                string? fileId = null;

                // 开启事务
                var result = await _db.Ado.UseTranAsync(async () =>
                {
                    mediaStorageEntity.Ref_Count++;
                    var bol = await _mediaStorageRepository.UpdateMediaStorageRefCount(mediaStorageEntity.Id, mediaStorageEntity.Ref_Count);
                    if (!bol) throw new Exception("更新引用计数失败");

                    var mediaFileEntity = new MediaFileEntity
                    {
                        File_Id = Guid.NewGuid().ToString("N"),
                        Storage_Id = mediaStorageEntity.Id,
                        User_Id = userId,
                        File_Type = fileType,
                        Original_Name = fileName,
                        Width = null,
                        Height = null,
                        Duration = null,
                        Status = 1,
                        Created_At = DateTime.UtcNow,
                    };
                    var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                    if (id <= 0) throw new Exception("创建文件记录失败");

                    fileId = mediaFileEntity.File_Id;
                });

                // ---------- 事务结束 ----------
                if (!result.IsSuccess)
                {
                    return new (false, result.ErrorMessage);
                }

                return new (true, null, fileId);
            }

            return new (false, "storage is not available ");
        }




        public async Task<UploadMediaFileReturn> UploadImageToLocal(long userId, IFormFile file)
        {
            var tempPath = Path.GetTempFileName();
            var filePathWithBucket = string.Empty;
            var thumbFilePathWithBucket = string.Empty;
            var compressFilePathWithBucket = string.Empty;
            Exception? e = null;
            string? fileId = null;

            var ext = Path.GetExtension(file.FileName) ?? ".jpg";
            var utcNow = DateTime.UtcNow;

            var bucket = _configuration.GetSection("MediaStorage:Local:Bucket").Value ?? throw new Exception("MediaStorage:Local:Bucket is not configured");
            var path = _configuration.GetSection("MediaStorage:Local:Path").Value ?? throw new Exception("MediaStorage:Local:Path is not configured");
            var basePath = Path.Combine(path, utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            MediaFileEntity mediaFileEntity;
            try
            {
                // 保存文件到临时位置并计算SHA256
                var sha256 = await SaveAndHashAsync(tempPath, file);

                var mediaStorageEntity = await _mediaStorageRepository.GetMediaStorageByFileSHA256(sha256);
                if (mediaStorageEntity != null && mediaStorageEntity.Status == 1)
                {
                    // 文件已存在且可用，增加引用计数

                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        mediaStorageEntity.Ref_Count++;
                        var bol = await _mediaStorageRepository.UpdateMediaStorageRefCount(mediaStorageEntity.Id, mediaStorageEntity.Ref_Count);
                        if (!bol) throw new Exception("更新引用计数失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Image,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"增加引用计数失败：{result.ErrorMessage}");
                }
                else if (mediaStorageEntity != null && mediaStorageEntity.Status == 0)
                {
                    // 文件已存在但已删除，恢复文件并更新数据库记录


                    #region 将临时文件移动到目标位置

                    var fileName = $"{Guid.NewGuid():N}{ext}";

                    var filePathWithoutBucket = Path.Combine(basePath, "image", fileName);
                    filePathWithBucket = Path.Combine(bucket, filePathWithoutBucket);

                    var dir1 = Path.GetDirectoryName(filePathWithBucket);
                    if (!Directory.Exists(dir1)) Directory.CreateDirectory(dir1!);

                    File.Move(tempPath, filePathWithBucket);

                    #endregion


                    #region 获取目标缩略图

                    var thumbFileName = $"{Guid.NewGuid():N}{ext}";

                    var thumbFilePathWithoutBucket = Path.Combine(basePath, "thumb", thumbFileName);
                    thumbFilePathWithBucket = Path.Combine(bucket, thumbFilePathWithoutBucket);

                    var dir2 = Path.GetDirectoryName(thumbFilePathWithBucket);
                    if (!Directory.Exists(dir2)) Directory.CreateDirectory(dir2!);

                    var thumbLength = await CreateResizeImage(filePathWithBucket, thumbFilePathWithBucket, 150);

                    #endregion


                    #region 获取压缩图

                    var compressFileName = $"{Guid.NewGuid():N}{ext}";

                    var compressFilePathWithoutBucket = Path.Combine(basePath, "compress", compressFileName);
                    compressFilePathWithBucket = Path.Combine(bucket, compressFilePathWithoutBucket);

                    var dir3 = Path.GetDirectoryName(compressFilePathWithBucket);
                    if (!Directory.Exists(dir3)) Directory.CreateDirectory(dir3!);

                    var compressLength = await CreateResizeImage(filePathWithBucket, compressFilePathWithBucket, 800);

                    #endregion


                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        // 统一 Windows 和 Linux 的数据库存储格式
                        var _filePathWithoutBucket = filePathWithoutBucket.Replace("\\", "/");
                        var _thumbFilePathWithoutBucket = thumbFilePathWithoutBucket.Replace("\\", "/");
                        var _compressFilePathWithoutBucket = compressFilePathWithoutBucket.Replace("\\", "/");

                        mediaStorageEntity.File_Path = _filePathWithoutBucket;
                        mediaStorageEntity.Storage_Bucket = bucket;
                        mediaStorageEntity.Storage_Type = StorageType.Local;
                        mediaStorageEntity.Ref_Count = 1;
                        mediaStorageEntity.Status = 1;
                        var bol1 = await _mediaStorageRepository.UpdateMediaStorage(mediaStorageEntity);
                        if (!bol1) throw new Exception("恢复文件记录失败");

                        var bol2 = await _mediaStorageVariantRepository.UpdateMediaStorageVariant(mediaStorageEntity.Id, VariantType.Thumbnail, _thumbFilePathWithoutBucket, file.ContentType, bucket, StorageType.Local, 1);
                        if (!bol2) throw new Exception("更新缩略图记录失败");

                        var bol3 = await _mediaStorageVariantRepository.UpdateMediaStorageVariant(mediaStorageEntity.Id, VariantType.Compress, _compressFilePathWithoutBucket, file.ContentType, bucket, StorageType.Local, 1);
                        if (!bol3) throw new Exception("更新压缩图记录失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Image,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"更新数据库记录失败：{result.ErrorMessage}");
                }
                else
                {
                    // 文件不存在，移动文件到目标位置并创建数据库记录


                    #region 将临时文件移动到目标位置

                    var fileName = $"{Guid.NewGuid():N}{ext}";

                    var filePathWithoutBucket = Path.Combine(basePath, "image", fileName);
                    filePathWithBucket = Path.Combine(bucket, filePathWithoutBucket);

                    var dir1 = Path.GetDirectoryName(filePathWithBucket);
                    if (!Directory.Exists(dir1)) Directory.CreateDirectory(dir1!);

                    File.Move(tempPath, filePathWithBucket);

                    #endregion


                    #region 获取目标缩略图

                    var thumbFileName = $"{Guid.NewGuid():N}{ext}";

                    var thumbFilePathWithoutBucket = Path.Combine(basePath, "thumb", thumbFileName);
                    thumbFilePathWithBucket = Path.Combine(bucket, thumbFilePathWithoutBucket);

                    var dir2 = Path.GetDirectoryName(thumbFilePathWithBucket);
                    if (!Directory.Exists(dir2)) Directory.CreateDirectory(dir2!);

                    var thumbLength = await CreateResizeImage(filePathWithBucket, thumbFilePathWithBucket, 150);

                    #endregion


                    #region 获取压缩图

                    var compressFileName = $"{Guid.NewGuid():N}{ext}";

                    var compressFilePathWithoutBucket = Path.Combine(basePath, "compress", compressFileName);
                    compressFilePathWithBucket = Path.Combine(bucket, compressFilePathWithoutBucket);

                    var dir3 = Path.GetDirectoryName(compressFilePathWithBucket);
                    if (!Directory.Exists(dir3)) Directory.CreateDirectory(dir3!);

                    var compressLength = await CreateResizeImage(filePathWithBucket, compressFilePathWithBucket, 800);

                    #endregion


                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        // 统一 Windows 和 Linux 的数据库存储格式
                        var _filePathWithoutBucket = filePathWithoutBucket.Replace("\\", "/");
                        var _thumbFilePathWithoutBucket = thumbFilePathWithoutBucket.Replace("\\", "/");
                        var _compressFilePathWithoutBucket = compressFilePathWithoutBucket.Replace("\\", "/");

                        mediaStorageEntity = new MediaStorageEntity
                        {
                            File_SHA256 = sha256,
                            File_Path = _filePathWithoutBucket,
                            File_Size = file.Length,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Ref_Count = 1,
                            Variant_Mask = (int)VariantType.Thumbnail | (int)VariantType.Compress,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id1 = await _mediaStorageRepository.CreateMediaStorage(mediaStorageEntity);
                        if (id1 <= 0) throw new Exception("创建文件记录失败");

                        var thumb_mediaStorageVariantEntity = new MediaStorageVariantEntity
                        {
                            Storage_Id = mediaStorageEntity.Id,
                            Variant_Type = VariantType.Thumbnail,
                            File_Path = _thumbFilePathWithoutBucket,
                            File_Size = thumbLength,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id2 = await _mediaStorageVariantRepository.CreateMediaStorageVariant(thumb_mediaStorageVariantEntity);
                        if (id2 <= 0) throw new Exception("创建缩略图记录失败");

                        var compress_mediaStorageVariantEntity = new MediaStorageVariantEntity
                        {
                            Storage_Id = mediaStorageEntity.Id,
                            Variant_Type = VariantType.Compress,
                            File_Path = _compressFilePathWithoutBucket,
                            File_Size = compressLength,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id3 = await _mediaStorageVariantRepository.CreateMediaStorageVariant(compress_mediaStorageVariantEntity);
                        if (id3 <= 0) throw new Exception("创建压缩图记录失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Image,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"创建数据库记录失败：{result.ErrorMessage}");
                }

            }
            catch (Exception ex)
            {
                e = ex;

                if (!string.IsNullOrEmpty(filePathWithBucket)) File.Delete(filePathWithBucket);
                if (!string.IsNullOrEmpty(thumbFilePathWithBucket)) File.Delete(thumbFilePathWithBucket);
                if (!string.IsNullOrEmpty(compressFilePathWithBucket)) File.Delete(compressFilePathWithBucket);
            }
            finally
            {
                File.Delete(tempPath);
            }

            return new (e == null, e?.Message, fileId);
        }

        public async Task<UploadMediaFileReturn> UploadImageToCOS(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public async Task<UploadMediaFileReturn> UploadImageToOSS(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public async Task<UploadMediaFileReturn> UploadImageToS3(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }





        public async Task<UploadMediaFileReturn> UploadVideoToLocal(long userId, IFormFile file)
        {
            var tempPath = Path.GetTempFileName();
            var filePathWithBucket = string.Empty;
            var coverFilePathWithBucket = string.Empty;
            var compressFilePathWithBucket = string.Empty;
            Exception? e = null;
            string? fileId = null;

            var ext = Path.GetExtension(file.FileName) ?? ".mp4";
            var utcNow = DateTime.UtcNow;

            var bucket = _configuration.GetSection("MediaStorage:Local:Bucket").Value ?? throw new Exception("MediaStorage:Local:Bucket is not configured");
            var path = _configuration.GetSection("MediaStorage:Local:Path").Value ?? throw new Exception("MediaStorage:Local:Path is not configured");
            var basePath = Path.Combine(path, utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            MediaFileEntity mediaFileEntity;
            try
            {
                // 保存文件到临时位置并计算SHA256
                var sha256 = await SaveAndHashAsync(tempPath, file);

                var mediaStorageEntity = await _mediaStorageRepository.GetMediaStorageByFileSHA256(sha256);
                if (mediaStorageEntity != null && mediaStorageEntity.Status == 1)
                {
                    // 文件已存在且可用，增加引用计数

                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        mediaStorageEntity.Ref_Count++;
                        var bol = await _mediaStorageRepository.UpdateMediaStorageRefCount(mediaStorageEntity.Id, mediaStorageEntity.Ref_Count);
                        if (!bol) throw new Exception("更新引用计数失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Video,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"增加引用计数失败：{result.ErrorMessage}");
                }
                else if (mediaStorageEntity != null && mediaStorageEntity.Status == 0)
                {
                    // 文件已存在但已删除，恢复文件并更新数据库记录


                    #region 将临时文件移动到目标位置

                    var fileName = $"{Guid.NewGuid():N}{ext}";

                    var filePathWithoutBucket = Path.Combine(basePath, "video", fileName);
                    filePathWithBucket = Path.Combine(bucket, filePathWithoutBucket);

                    var dir1 = Path.GetDirectoryName(filePathWithBucket);
                    if (!Directory.Exists(dir1)) Directory.CreateDirectory(dir1!);

                    File.Move(tempPath, filePathWithBucket);

                    #endregion


                    #region 获取封面图

                    var coverFileName = $"{Guid.NewGuid():N}.jpg";

                    var coverFilePathWithoutBucket = Path.Combine(basePath, "cover", coverFileName);
                    coverFilePathWithBucket = Path.Combine(bucket, coverFilePathWithoutBucket);

                    var dir2 = Path.GetDirectoryName(coverFilePathWithBucket);
                    if (!Directory.Exists(dir2)) Directory.CreateDirectory(dir2!);

                    var coverLength = await CreateVideoCover(filePathWithBucket, coverFilePathWithBucket, 5);

                    #endregion


                    #region 获取压缩视频

                    //var compressFileName = $"{Guid.NewGuid():N}{ext}";

                    //var compressFilePathWithoutBucket = Path.Combine(basePath, "video_L", compressFileName);
                    //compressFilePathWithBucket = Path.Combine(bucket, compressFilePathWithoutBucket);

                    //var dir3 = Path.GetDirectoryName(compressFilePathWithBucket);
                    //if (!Directory.Exists(dir3)) Directory.CreateDirectory(dir3!);

                    //var compressLength = await Compress2Video(filePathWithBucket, compressFilePathWithBucket);

                    #endregion


                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        // 统一 Windows 和 Linux 的数据库存储格式
                        var _filePathWithoutBucket = filePathWithoutBucket.Replace("\\", "/");
                        var _coverFilePathWithoutBucket = coverFilePathWithoutBucket.Replace("\\", "/");
                        //var _compressFilePathWithoutBucket = compressFilePathWithoutBucket.Replace("\\", "/");

                        mediaStorageEntity.File_Path = _filePathWithoutBucket;
                        mediaStorageEntity.Storage_Bucket = bucket;
                        mediaStorageEntity.Storage_Type = StorageType.Local;
                        mediaStorageEntity.Ref_Count = 1;
                        mediaStorageEntity.Status = 1;
                        var bol1 = await _mediaStorageRepository.UpdateMediaStorage(mediaStorageEntity);
                        if (!bol1) throw new Exception("恢复文件记录失败");

                        var bol2 = await _mediaStorageVariantRepository.UpdateMediaStorageVariant(mediaStorageEntity.Id, VariantType.Cover, _coverFilePathWithoutBucket, file.ContentType, bucket, StorageType.Local, 1);
                        if (!bol2) throw new Exception("更新封面图记录失败");

                        //var bol3 = await _mediaStorageVariantRepository.UpdateMediaStorageVariant(mediaStorageEntity.Id, VariantType.LowVideo, _compressFilePathWithoutBucket, file.ContentType, bucket, StorageType.Local, 1);
                        //if (!bol3) throw new Exception("更新压缩视频记录失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Video,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"更新数据库记录失败：{result.ErrorMessage}");
                }
                else
                {
                    // 文件不存在，移动文件到目标位置并创建数据库记录


                    #region 将临时文件移动到目标位置

                    var fileName = $"{Guid.NewGuid():N}{ext}";

                    var filePathWithoutBucket = Path.Combine(basePath, "video", fileName);
                    filePathWithBucket = Path.Combine(bucket, filePathWithoutBucket);

                    var dir1 = Path.GetDirectoryName(filePathWithBucket);
                    if (!Directory.Exists(dir1)) Directory.CreateDirectory(dir1!);

                    File.Move(tempPath, filePathWithBucket);

                    #endregion


                    #region 获取封面图

                    var coverFileName = $"{Guid.NewGuid():N}.jpg";

                    var coverFilePathWithoutBucket = Path.Combine(basePath, "cover", coverFileName);
                    coverFilePathWithBucket = Path.Combine(bucket, coverFilePathWithoutBucket);

                    var dir2 = Path.GetDirectoryName(coverFilePathWithBucket);
                    if (!Directory.Exists(dir2)) Directory.CreateDirectory(dir2!);

                    var coverLength = await CreateVideoCover(filePathWithBucket, coverFilePathWithBucket, 5);

                    #endregion


                    #region 获取压缩视频

                    //var compressFileName = $"{Guid.NewGuid():N}{ext}";

                    //var compressFilePathWithoutBucket = Path.Combine(basePath, "video_L", compressFileName);
                    //compressFilePathWithBucket = Path.Combine(bucket, compressFilePathWithoutBucket);

                    //var dir3 = Path.GetDirectoryName(compressFilePathWithBucket);
                    //if (!Directory.Exists(dir3)) Directory.CreateDirectory(dir3!);

                    //var compressLength = await Compress2Video(filePathWithBucket, compressFilePathWithBucket);

                    #endregion


                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        // 统一 Windows 和 Linux 的数据库存储格式
                        var _filePathWithoutBucket = filePathWithoutBucket.Replace("\\", "/");
                        var _coverFilePathWithoutBucket = coverFilePathWithoutBucket.Replace("\\", "/");
                        //var _compressFilePathWithoutBucket = compressFilePathWithoutBucket.Replace("\\", "/");

                        mediaStorageEntity = new MediaStorageEntity
                        {
                            File_SHA256 = sha256,
                            File_Path = _filePathWithoutBucket,
                            File_Size = file.Length,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Ref_Count = 1,
                            Variant_Mask = (int)VariantType.Cover | (int)VariantType.LowVideo,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id1 = await _mediaStorageRepository.CreateMediaStorage(mediaStorageEntity);
                        if (id1 <= 0) throw new Exception("创建文件记录失败");

                        var cover_mediaStorageVariantEntity = new MediaStorageVariantEntity
                        {
                            Storage_Id = mediaStorageEntity.Id,
                            Variant_Type = VariantType.Cover,
                            File_Path = _coverFilePathWithoutBucket,
                            File_Size = coverLength,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id2 = await _mediaStorageVariantRepository.CreateMediaStorageVariant(cover_mediaStorageVariantEntity);
                        if (id2 <= 0) throw new Exception("创建封面图记录失败");

                        //var compress_mediaStorageVariantEntity = new MediaStorageVariantEntity
                        //{
                        //    Storage_Id = mediaStorageEntity.Id,
                        //    Variant_Type = VariantType.LowVideo,
                        //    File_Path = _compressFilePathWithoutBucket,
                        //    File_Size = compressLength,
                        //    Mime_Type = file.ContentType,
                        //    Storage_Bucket = bucket,
                        //    Storage_Type = StorageType.Local,
                        //    Status = 1,
                        //    Created_At = DateTime.UtcNow,
                        //};
                        //var id3 = await _mediaStorageVariantRepository.CreateMediaStorageVariant(compress_mediaStorageVariantEntity);
                        //if (id3 <= 0) throw new Exception("创建压缩视频记录失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Video,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"创建数据库记录失败：{result.ErrorMessage}");
                }

            }
            catch (Exception ex)
            {
                e = ex;

                if (!string.IsNullOrEmpty(filePathWithBucket)) File.Delete(filePathWithBucket);
                if (!string.IsNullOrEmpty(coverFilePathWithBucket)) File.Delete(coverFilePathWithBucket);
                if (!string.IsNullOrEmpty(compressFilePathWithBucket)) File.Delete(compressFilePathWithBucket);
            }
            finally
            {
                File.Delete(tempPath);
            }

            return new (e == null, e?.Message, fileId);
        }

        public async Task<UploadMediaFileReturn> UploadVideoToCOS(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public async Task<UploadMediaFileReturn> UploadVideoToOSS(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public async Task<UploadMediaFileReturn> UploadVideoToS3(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }




        public async Task<UploadMediaFileReturn> UploadHeadToLocal(long userId, IFormFile file)
        {
            var tempPath = Path.GetTempFileName();
            var filePathWithBucket = string.Empty;
            var thumbFilePathWithBucket = string.Empty;
            Exception? e = null;
            string? fileId = null;

            var ext = Path.GetExtension(file.FileName) ?? ".jpg";
            var utcNow = DateTime.UtcNow;

            var bucket = _configuration.GetSection("MediaStorage:Local:Bucket").Value ?? throw new Exception("MediaStorage:Local:Bucket is not configured");
            var path = _configuration.GetSection("MediaStorage:Local:Path").Value ?? throw new Exception("MediaStorage:Local:Path is not configured");
            var basePath = Path.Combine(path, utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            MediaFileEntity mediaFileEntity;
            try
            {
                // 保存文件到临时位置并计算SHA256
                var sha256 = await SaveAndHashAsync(tempPath, file);

                var mediaStorageEntity = await _mediaStorageRepository.GetMediaStorageByFileSHA256(sha256);
                if (mediaStorageEntity != null && mediaStorageEntity.Status == 1)
                {
                    // 文件已存在且可用，增加引用计数

                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        mediaStorageEntity.Ref_Count++;
                        var bol = await _mediaStorageRepository.UpdateMediaStorageRefCount(mediaStorageEntity.Id, mediaStorageEntity.Ref_Count);
                        if (!bol) throw new Exception("更新引用计数失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Image,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"增加引用计数失败：{result.ErrorMessage}");
                }
                else if (mediaStorageEntity != null && mediaStorageEntity.Status == 0)
                {
                    // 文件已存在但已删除，恢复文件并更新数据库记录


                    #region 将临时文件移动到目标位置

                    var fileName = $"{Guid.NewGuid():N}{ext}";

                    var filePathWithoutBucket = Path.Combine(basePath, "image", fileName);
                    filePathWithBucket = Path.Combine(bucket, filePathWithoutBucket);

                    var dir1 = Path.GetDirectoryName(filePathWithBucket);
                    if (!Directory.Exists(dir1)) Directory.CreateDirectory(dir1!);

                    File.Move(tempPath, filePathWithBucket);

                    #endregion


                    #region 获取目标头像

                    var thumbFileName = $"{Guid.NewGuid():N}{ext}";

                    var thumbFilePathWithoutBucket = Path.Combine(basePath, "thumb", thumbFileName);
                    thumbFilePathWithBucket = Path.Combine(bucket, thumbFilePathWithoutBucket);

                    var dir2 = Path.GetDirectoryName(thumbFilePathWithBucket);
                    if (!Directory.Exists(dir2)) Directory.CreateDirectory(dir2!);

                    var thumbLength = await CreateResizeImage(filePathWithBucket, thumbFilePathWithBucket, 128, 80);

                    #endregion

                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        // 统一 Windows 和 Linux 的数据库存储格式
                        var _filePathWithoutBucket = filePathWithoutBucket.Replace("\\", "/");
                        var _thumbFilePathWithoutBucket = thumbFilePathWithoutBucket.Replace("\\", "/");

                        mediaStorageEntity.File_Path = _filePathWithoutBucket;
                        mediaStorageEntity.Storage_Bucket = bucket;
                        mediaStorageEntity.Storage_Type = StorageType.Local;
                        mediaStorageEntity.Ref_Count = 1;
                        mediaStorageEntity.Status = 1;
                        var bol1 = await _mediaStorageRepository.UpdateMediaStorage(mediaStorageEntity);
                        if (!bol1) throw new Exception("恢复文件记录失败");

                        var bol2 = await _mediaStorageVariantRepository.UpdateMediaStorageVariant(mediaStorageEntity.Id, VariantType.Thumbnail, _thumbFilePathWithoutBucket, file.ContentType, bucket, StorageType.Local, 1);
                        if (!bol2) throw new Exception("更新缩略图记录失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Image,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"更新数据库记录失败：{result.ErrorMessage}");
                }
                else
                {
                    // 文件不存在，移动文件到目标位置并创建数据库记录


                    #region 将临时文件移动到目标位置

                    var fileName = $"{Guid.NewGuid():N}{ext}";

                    var filePathWithoutBucket = Path.Combine(basePath, "image", fileName);
                    filePathWithBucket = Path.Combine(bucket, filePathWithoutBucket);

                    var dir1 = Path.GetDirectoryName(filePathWithBucket);
                    if (!Directory.Exists(dir1)) Directory.CreateDirectory(dir1!);

                    File.Move(tempPath, filePathWithBucket);

                    #endregion


                    #region 获取目标头像

                    var thumbFileName = $"{Guid.NewGuid():N}{ext}";

                    var thumbFilePathWithoutBucket = Path.Combine(basePath, "thumb", thumbFileName);
                    thumbFilePathWithBucket = Path.Combine(bucket, thumbFilePathWithoutBucket);

                    var dir2 = Path.GetDirectoryName(thumbFilePathWithBucket);
                    if (!Directory.Exists(dir2)) Directory.CreateDirectory(dir2!);

                    var thumbLength = await CreateResizeImage(filePathWithBucket, thumbFilePathWithBucket, 128, 80);

                    #endregion


                    // 开启事务
                    var result = await _db.Ado.UseTranAsync(async () =>
                    {
                        // 统一 Windows 和 Linux 的数据库存储格式
                        var _filePathWithoutBucket = filePathWithoutBucket.Replace("\\", "/");
                        var _thumbFilePathWithoutBucket = thumbFilePathWithoutBucket.Replace("\\", "/");

                        mediaStorageEntity = new MediaStorageEntity
                        {
                            File_SHA256 = sha256,
                            File_Path = _filePathWithoutBucket,
                            File_Size = file.Length,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Ref_Count = 1,
                            Variant_Mask = (int)VariantType.Thumbnail,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id1 = await _mediaStorageRepository.CreateMediaStorage(mediaStorageEntity);
                        if (id1 <= 0) throw new Exception("创建文件记录失败");

                        var thumb_mediaStorageVariantEntity = new MediaStorageVariantEntity
                        {
                            Storage_Id = mediaStorageEntity.Id,
                            Variant_Type = VariantType.Thumbnail,
                            File_Path = _thumbFilePathWithoutBucket,
                            File_Size = thumbLength,
                            Mime_Type = file.ContentType,
                            Storage_Bucket = bucket,
                            Storage_Type = StorageType.Local,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id2 = await _mediaStorageVariantRepository.CreateMediaStorageVariant(thumb_mediaStorageVariantEntity);
                        if (id2 <= 0) throw new Exception("创建缩略图记录失败");

                        mediaFileEntity = new MediaFileEntity
                        {
                            File_Id = Guid.NewGuid().ToString("N"),
                            Storage_Id = mediaStorageEntity.Id,
                            User_Id = userId,
                            File_Type = FileType.Image,
                            Original_Name = Path.GetFileName(file.FileName),
                            Width = null,
                            Height = null,
                            Duration = null,
                            Status = 1,
                            Created_At = DateTime.UtcNow,
                        };
                        var id = await _mediaFileRepository.CreateMediaFile(mediaFileEntity);
                        if (id <= 0) throw new Exception("创建文件记录失败");

                        fileId = mediaFileEntity.File_Id;
                    });

                    // ---------- 事务结束 ----------
                    if (!result.IsSuccess) throw new Exception($"创建数据库记录失败：{result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                e = ex;

                if (!string.IsNullOrEmpty(filePathWithBucket)) File.Delete(filePathWithBucket);
                if (!string.IsNullOrEmpty(thumbFilePathWithBucket)) File.Delete(thumbFilePathWithBucket);
            }
            finally
            {
                File.Delete(tempPath);
            }

            return new (e == null, e?.Message, fileId);
        }

        public async Task<UploadMediaFileReturn> UploadHeadToCOS(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public async Task<UploadMediaFileReturn> UploadHeadToOSS(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public async Task<UploadMediaFileReturn> UploadHeadToS3(long userId, IFormFile file)
        {
            throw new NotImplementedException();
        }





        public async Task<GetMediaStorageReturn> GetMediaStorageByFileId(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId)) return new(false, "File ID is required");

            var mediaFileEntity = await _mediaFileRepository.GetFileByFileId(fileId);
            if (mediaFileEntity == null) return new(false, "File not found");

            var mediaStorageEntity = await _mediaStorageRepository.GetMediaStorageById(mediaFileEntity.Storage_Id);
            if (mediaStorageEntity == null) return new(true, IsExist: false);
            else if (mediaStorageEntity.Status != 1) return new(true, IsExist: false);

            var type = mediaStorageEntity.Storage_Type;
            var path = GetFileUrl(mediaStorageEntity.Storage_Type, mediaStorageEntity.Storage_Bucket, mediaStorageEntity.File_Path);
            var mimeType = mediaStorageEntity.Mime_Type;
            var fileDownloadName = Path.GetFileName(path);
            return new(true, null, true, mediaStorageEntity.Id, type, path, mimeType, fileDownloadName);
        }

        public async Task<GetMediaStorageReturn> GetMediaStorageVariantByFileId(string fileId, VariantType variantType)
        {
            if (string.IsNullOrWhiteSpace(fileId)) return new(false, "File ID is required");

            var mediaFileEntity = await _mediaFileRepository.GetFileByFileId(fileId);
            if (mediaFileEntity == null) return new(false, "File not found");

            var mediaStorageVariantEntity = await _mediaStorageVariantRepository.GetMediaStorageVariantByStorageIdAndVariantType(mediaFileEntity.Storage_Id, variantType);
            if (mediaStorageVariantEntity == null) return new(true, IsExist: false);
            else if (mediaStorageVariantEntity.Status != 1) return new(true, IsExist: false);

            var type = mediaStorageVariantEntity.Storage_Type;
            var path = GetFileUrl(mediaStorageVariantEntity.Storage_Type, mediaStorageVariantEntity.Storage_Bucket, mediaStorageVariantEntity.File_Path);
            var mimeType = mediaStorageVariantEntity.Mime_Type;
            var fileDownloadName = Path.GetFileName(path);
            return new(true, null, true, mediaStorageVariantEntity.Id, type, path, mimeType, fileDownloadName);
        }

        public async Task<GetMediaStorageReturn> GetMediaStorageByFileSHA256(string fileSHA, long size)
        {
            if (string.IsNullOrWhiteSpace(fileSHA)) return new(false, "File SHA256 is required");

            var mediaStorageEntity = await _mediaStorageRepository.GetMediaStorageByFileSHA256(fileSHA);
            if (mediaStorageEntity == null) return new(true, IsExist: false);
            else if (mediaStorageEntity.File_Size != size) return new(false, "File size does not match");
            else if (mediaStorageEntity.Status != 1) return new(true, IsExist: false);

            var type = mediaStorageEntity.Storage_Type;
            var path = GetFileUrl(mediaStorageEntity.Storage_Type, mediaStorageEntity.Storage_Bucket, mediaStorageEntity.File_Path);
            var mimeType = mediaStorageEntity.Mime_Type;
            var fileDownloadName = Path.GetFileName(path);
            return new(true, null, true, mediaStorageEntity.Id, type, path, mimeType, fileDownloadName);
        }





        private string GetFileUrl(StorageType storageType, string bucket, string path)
        {
            return storageType switch
            {
                StorageType.Local => Path.Combine([bucket, .. path.Split('/')]),
                StorageType.OSS => $"https://{bucket}.oss-cn-shanghai.aliyuncs.com/{path}",
                StorageType.COS => $"https://{bucket}.cos.ap-guangzhou.myqcloud.com/{path}",
                StorageType.S3 => $"https://{bucket}.blob.core.windows.net/{path}",
                _ => throw new Exception("unknown storage type")
            };
        }

        private const int BufferSize = 81920; // .NET推荐

        private async Task<string> SaveAndHashAsync(string savePath, IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("file is null or empty");

            using var sha256 = SHA256.Create();
            using var input = file.OpenReadStream();
            using var output = File.Create(savePath);

            var buffer = new byte[BufferSize];
            int read;

            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);          // 写入文件
                sha256.TransformBlock(buffer, 0, read, null, 0);                // 计算 SHA256
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        }

        private async Task<long> CreateResizeImage(string inputPath, string outputPath, int? width = null, int? quality = null)
        {
            using var input = File.OpenRead(inputPath);
            using var original = SKBitmap.Decode(input);

            if (original == null) throw new ArgumentNullException("图片解析失败");

            width ??= original.Width;
            quality ??= 80;

            var ratio = (float)width / original.Width;
            var height = (int)(original.Height * ratio);

            using var resized = original.Resize(new SKImageInfo(width.Value, height), new SKSamplingOptions(SKFilterMode.Linear));

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality.Value);

            using var output = File.OpenWrite(outputPath);
            data.SaveTo(output);

            return output.Length;
        }

        private async Task<long> CreateVideoCover(string inputPath, string outputPath, double timeSeconds = 5)
        {
            var path = _configuration.GetSection("FFmpeg:Path").Value ?? throw new Exception("FFmpeg:Path is not configured");

            var helper = new FFmpegHelper(path);

            if (!helper.IsFFmpegAvailable()) throw new Exception("未找到 ffmpeg");

            var success = await helper.ExtractFrameAsync(inputPath, outputPath, timeSeconds);
            if (success)
            {
                using var output = File.OpenWrite(outputPath);
                return output.Length;
            }
            else
            {
                throw new Exception("提取视频帧失败");
            }
        }

        private async Task<long> Compress1Video(string inputPath, string outputPath)
        {
            var path = _configuration.GetSection("FFmpeg:Path").Value ?? throw new Exception("FFmpeg:Path is not configured");

            var helper = new FFmpegHelper(path);

            if (!helper.IsFFmpegAvailable()) throw new Exception("未找到 ffmpeg");

            var videoBitrate = "1.5M";
            var audioBitrate = "128k";

            var success = await helper.CompressVideoWithBitrateAsync(inputPath, outputPath, videoBitrate, audioBitrate ?? "copy");
            if (success)
            {
                using var output = File.OpenWrite(outputPath);
                return output.Length;
            }
            else
            {
                throw new Exception("压缩视频失败");
            }
        }

        private async Task<long> Compress2Video(string inputPath, string outputPath)
        {
            var path = _configuration.GetSection("FFmpeg:Path").Value ?? throw new Exception("FFmpeg:Path is not configured");

            var helper = new FFmpegHelper(path);

            if (!helper.IsFFmpegAvailable()) throw new Exception("未找到 ffmpeg");

            var crf = 23;
            var preset = "fast";
            var audioBitrate = "128k";

            var success = await helper.CompressVideoAsync(inputPath, outputPath, crf, preset, audioBitrate ?? "copy");
            if (success)
            {
                using var output = File.OpenWrite(outputPath);
                return output.Length;
            }
            else
            {
                throw new Exception("压缩视频失败");
            }
        }


    }
}
