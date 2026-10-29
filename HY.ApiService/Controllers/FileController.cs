using HY.ApiService.Controllers.Requests;
using HY.ApiService.Enums;
using HY.ApiService.Extensions;
using HY.ApiService.Models;
using HY.ApiService.Services;
using HY.ApiService.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using OracleInternal.Secure.Network;
using SkiaSharp;
using System.Buffers;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileController : ControllerBase
    {
        readonly IChatNotificationService _chatNotificationService;
        readonly IRedisFileService _redisFileService;

        readonly IMediaService _mediaService;
        readonly IConfiguration _configuration;

        public FileController(IChatNotificationService chatNotificationService, IRedisFileService redisFileService, IMediaService mediaService, IConfiguration configuration)
        {
            _chatNotificationService = chatNotificationService;
            _redisFileService = redisFileService;

            _mediaService = mediaService;
            _configuration = configuration;
        }



        [Authorize]
        [HttpPost("challenge")]
        public async Task<IActionResult> Challenge(string fileSHA256, long size)
        {
            if (string.IsNullOrWhiteSpace(fileSHA256))
            {
                return Ok(new Response(false, "error File SHA256"));
            }
            else if (size <= 0)
            {
                return Ok(new Response(false, "error Size"));
            }

            if (size <= 1024 * 1024)    // 小于等于1MB的文件直接上传，不需要挑战
            {
                return Ok(new Response(true)
                {
                    Data = new Dictionary<string, object?>
                    {
                        { "IsExist", false }
                    }
                });
            }

            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _mediaService.GetMediaStorageByFileSHA256(fileSHA256, size);
            if (!result.IsSucc)
            {
                return Ok(new Response(false, result.Error));
            }

            if (result.IsExist == null)
            {
                throw new ArgumentException("IsExist is null");
            }

            if (!result.IsExist.Value)
            {
                return Ok(new Response(true)
                {
                    Data = new Dictionary<string, object?>
                    {
                        { "IsExist", false }
                    }
                });
            }

            var fileChallenge = await FileChallengeGenerator.GenerateAsync(result.Path!);

            var challengeInfo = await _redisFileService.CreateFileChallenge(userId, result.StorageId!.Value, fileChallenge);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "IsExist", true },
                    { "ChallengeId", challengeInfo.ChallengeId },
                    { "BlockSize", fileChallenge.BlockSize },
                    { "BlockIndexes", fileChallenge.BlockIndexes },
                }
            });
        }

        [Authorize]
        [HttpPost("challenge/confirm")]
        public async Task<IActionResult> ChallengeConfirm(ChallengeConfirmRequest challengeConfirmRequest)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (challengeConfirmRequest == null || string.IsNullOrWhiteSpace(challengeConfirmRequest.ChallengeId))
            {
                return Ok(new Response(false, "error Challenge Confirm Request"));
            }

            var storageId = await _redisFileService.ConfirmFileChallenge(userId, challengeConfirmRequest.ChallengeId, challengeConfirmRequest.BlockMd5s);
            if (storageId == null)
            {
                return Ok(new Response(false, "Challenge fail"));
            }

            var result = await _mediaService.CreateMediaFile(userId, storageId.Value, challengeConfirmRequest.FileName, challengeConfirmRequest.FileType);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "File_Id", result.FileId },
                }
            });
        }




        [Authorize]
        [HttpPost("upload/head")]
        [RequestSizeLimit(1 * 1024 * 1024)]
        public async Task<IActionResult> UploadHead(IFormFile file)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            const long maxFileSize = 1 * 1024 * 1024; // 1 MiB

            if (file == null || file.Length == 0)
                return Ok(new Response(false, "File is required"));
            else if (file.Length > maxFileSize)
                return Ok(new Response(false, $"File size exceeds the limit of {maxFileSize} bytes"));

            var result = await _mediaService.UploadHead(userId, file);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "File_Id", result.FileId },
                }
            });
        }

        [Authorize]
        [HttpPost("upload/image")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            const long maxFileSize = 10 * 1024 * 1024; // 10 MiB

            if (!long.TryParse(Request.Headers["File-Size"], out long totalBytes) || totalBytes <= 0)
                return Ok(new Response(false, "Invalid file size"));

            if (!Guid.TryParse(Request.Headers["File-UploadId"], out Guid uploadId) || uploadId == Guid.Empty)
                return Ok(new Response(false, "Invalid upload ID"));


            var maxFileBytes = _configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize") ?? throw new Exception("MediaStorage:Type is not configured");
            if (totalBytes > maxFileBytes)
            {
                return Ok(new Response(false, $"上传文件超过允许的大小限制，最大允许 {maxFileBytes} 字节"));
            }

            // 接收文件并保存到临时文件，同时计算 SHA256
            var file = await SaveToTempAsync(totalBytes, uploadId);

            if (file == null || file.Length == 0)
                return Ok(new Response(false, "File is required"));
            else if (file.Length > maxFileSize)
                return Ok(new Response(false, $"File size exceeds the limit of {maxFileSize} bytes"));

            var result = await _mediaService.UploadImage(userId, file);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "File_Id", result.FileId },
                }
            });
        }

        [Authorize]
        [HttpPost("upload/video")]
        public async Task<IActionResult> UploadVideo()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);


            if (!long.TryParse(Request.Headers["File-Size"], out long totalBytes) || totalBytes <= 0)
                return Ok(new Response(false, "Invalid file size"));

            if (!Guid.TryParse(Request.Headers["File-UploadId"], out Guid uploadId) || uploadId == Guid.Empty)
                return Ok(new Response(false, "Invalid upload ID"));


            var maxFileBytes = _configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize") ?? throw new Exception("MediaStorage:Type is not configured");
            if (totalBytes > maxFileBytes)
            {
                return Ok(new Response(false, $"上传文件超过允许的大小限制，最大允许 {maxFileBytes} 字节"));
            }

            // 接收文件并保存到临时文件，同时计算 SHA256
            var file = await SaveToTempAsync(totalBytes, uploadId);

            if (file == null || file.Length == 0)
                return Ok(new Response(false, "File is required"));

            var result = await _mediaService.UploadVideo(userId, file);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "File_Id", result.FileId },
                }
            });
        }




        [HttpGet("image/origin/{fileId}")]
        public async Task<IActionResult> GetOriginImage(string fileId)
        {
            var result = await _mediaService.GetMediaStorageByFileId(fileId);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }

        [HttpGet("image/compress/{fileId}")]
        public async Task<IActionResult> GetCompressImage(string fileId)
        {
            var result = await _mediaService.GetMediaStorageVariantByFileId(fileId, VariantType.Compress);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }

        [HttpGet("image/thumb/{fileId}")]
        public async Task<IActionResult> GetThumbImage(string fileId)
        {
            var result = await _mediaService.GetMediaStorageVariantByFileId(fileId, VariantType.Thumbnail);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }




        [HttpGet("video/origin/{fileId}")]
        public async Task<IActionResult> GetOriginVideo(string fileId)
        {
            var result = await _mediaService.GetMediaStorageByFileId(fileId);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }

        [HttpGet("video/compress/{fileId}")]
        public async Task<IActionResult> GetCompressVideo(string fileId)
        {
            var result = await _mediaService.GetMediaStorageVariantByFileId(fileId, VariantType.LowVideo);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }

        [HttpGet("video/cover/{fileId}")]
        public async Task<IActionResult> GetCoverVideo(string fileId)
        {
            var result = await _mediaService.GetMediaStorageVariantByFileId(fileId, VariantType.Cover);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }




        [HttpGet("head/{fileId}")]
        public async Task<IActionResult> GetHead(string fileId)
        {
            var result = await _mediaService.GetMediaStorageVariantByFileId(fileId, VariantType.Thumbnail);
            if (!result.IsSucc || result.IsExist != true)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "public, max-age=31536000, immutable";      // 开启强缓存

            return result.StorageType switch
            {
                StorageType.Local => PhysicalFile(result.Path, result.ContentType, result.FileDownloadName, true),
                StorageType.OSS => Redirect(result.Path),
                StorageType.COS => Redirect(result.Path),
                StorageType.S3 => Redirect(result.Path),
                _ => NotFound()
            };
        }














        /// <summary>
        /// 从 multipart/form-data 请求中流式读取文件，
        /// 保存到系统临时文件，并计算 SHA256。
        /// </summary>
        private async Task<UploadedFile> SaveToTempAsync(long totalBytes, Guid uploadId, string fieldName = "file", CancellationToken cancellationToken = default)
        {
            var request = Request;
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            #region MyRegion
            ArgumentNullException.ThrowIfNull(request);

            // 检查 Content-Type
            if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) || mediaType is null || !string.Equals(mediaType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("请求格式必须为 multipart/form-data");
            }

            // 获取 multipart boundary
            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException("缺少 multipart boundary");
            }

            var reader = new MultipartReader(boundary, request.Body)
            {
                BodyLengthLimit = _configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize") ?? throw new Exception("Kestrel:Limits:MaxRequestBodySize is not configured")
            };
            #endregion

            string? tempFile = null;
            UploadedFile? result = null;
            bool completed = false;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);

            try
            {
                MultipartSection? section;

                while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
                {
                    #region MyRegion
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) || disposition is null)
                    {
                        await section.Body.CopyToAsync(Stream.Null, cancellationToken);

                        continue;
                    }

                    var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

                    var fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value;

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        fileName = HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
                    }

                    // 只处理指定的文件字段
                    if (!string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(fileName))
                    {
                        await section.Body.CopyToAsync(Stream.Null, cancellationToken);

                        continue;
                    }

                    // 一次请求只允许上传一个文件
                    if (result is not null)
                    {
                        throw new InvalidDataException("一次请求只能上传一个文件");
                    }
                    #endregion

                    // 仅保留文件名，不使用客户端提供的路径
                    fileName = Path.GetFileName(fileName.Replace('\\', '/'));

                    // 创建系统临时文件
                    tempFile = Path.GetTempFileName();

                    string contentType = section.ContentType ?? "application/octet-stream";

                    long length = 0;
                    string sha256;

                    using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    {
                        await using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 128 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            int read;

                            long lastReportedBytes = 0;
                            var lastReportedAt = Stopwatch.StartNew();

                            while ((read = await section.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var data = buffer.AsMemory(0, read);

                                // 写入临时文件
                                await output.WriteAsync(data, cancellationToken);

                                hash.AppendData(data.Span);
                                length += read;

                                bool shouldReport = length - lastReportedBytes >= 1024 * 1024 || lastReportedAt.ElapsedMilliseconds >= 300;
                                if (shouldReport)
                                {
                                    var progress = Math.Clamp((double)length / totalBytes * 100, 0, 100);
                                    await _chatNotificationService.UploadProgressNotify(userId, platform, uploadId, progress, cancellationToken);

                                    lastReportedBytes = length;
                                    lastReportedAt.Restart();
                                }
                            }

                            await output.FlushAsync(cancellationToken);
                        }

                        // 输出 64 位十六进制 SHA256
                        sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                    }

                    result = new UploadedFile
                    {
                        TempPath = tempFile,
                        FileName = fileName,
                        ContentType = contentType,
                        Length = length,
                        Sha256 = sha256
                    };
                }

                if (result is null)
                {
                    throw new InvalidDataException($"未找到名为 {fieldName} 的文件");
                }

                cancellationToken.ThrowIfCancellationRequested();

                completed = true;
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);

                // 接收失败、发生异常或取消时删除未完成的临时文件
                if (!completed && tempFile is not null)
                {
                    try
                    {
                        if (System.IO.File.Exists(tempFile))
                        {
                            System.IO.File.Delete(tempFile);
                        }
                    }
                    catch (IOException)
                    {
                        // 删除失败时，可由后台清理任务处理残留文件
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 当前进程没有删除权限
                    }
                }
            }
        }

    }
}
