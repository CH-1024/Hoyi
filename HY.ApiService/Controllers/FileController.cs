using HY.ApiService.Controllers.Requests;
using HY.ApiService.Enums;
using HY.ApiService.Extensions;
using HY.ApiService.Models;
using HY.ApiService.Services;
using HY.ApiService.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using OracleInternal.Secure.Network;
using SkiaSharp;
using System.Security.Claims;
using System.Security.Cryptography;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileController : ControllerBase
    {
        readonly IRedisFileService _redisFileService;

        readonly IMediaService _mediaService;
        readonly IConfiguration _configuration;

        public FileController(IRedisFileService redisFileService, IMediaService mediaService, IConfiguration configuration)
        {
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
        [HttpPost("upload/image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            var type = _configuration.GetSection("MediaStorage:Type").Value ?? throw new Exception("MediaStorage:Type is not configured");

            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            UploadMediaFileReturn result;

            if (file == null || file.Length == 0)
            {
                result = new (false, "File is required");
            }
            else if (type == "Local")
            {
                result = await _mediaService.UploadImageToLocal(userId, file);
            }
            else if (type == "OSS")
            {
                result = await _mediaService.UploadImageToOSS(userId, file);
            }
            else if (type == "COS")
            {
                result = await _mediaService.UploadImageToCOS(userId, file);
            }
            else if (type == "S3")
            {
                result = await _mediaService.UploadImageToS3(userId, file);
            }
            else
            {
                throw new Exception("Unsupported media storage type");
            }

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
        public async Task<IActionResult> UploadVideo(IFormFile file)
        {
            var type = _configuration.GetSection("MediaStorage:Type").Value ?? throw new Exception("MediaStorage:Type is not configured");

            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            UploadMediaFileReturn result;

            if (file == null || file.Length == 0)
            {
                result = new (false, "File is required");
            }
            else if (type == "Local")
            {
                result = await _mediaService.UploadVideoToLocal(userId, file);
            }
            else if (type == "OSS")
            {
                result = await _mediaService.UploadVideoToOSS(userId, file);
            }
            else if (type == "COS")
            {
                result = await _mediaService.UploadVideoToCOS(userId, file);
            }
            else if (type == "S3")
            {
                result = await _mediaService.UploadVideoToS3(userId, file);
            }
            else
            {
                throw new Exception("Unsupported media storage type");
            }

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
        public async Task<IActionResult> UploadHead(IFormFile file)
        {
            var type = _configuration.GetSection("MediaStorage:Type").Value ?? throw new Exception("MediaStorage:Type is not configured");

            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            UploadMediaFileReturn result;

            if (file == null || file.Length == 0)
            {
                result = new (false, "File is required");
            }
            else if (type == "Local")
            {
                result = await _mediaService.UploadHeadToLocal(userId, file);
            }
            else if (type == "OSS")
            {
                result = await _mediaService.UploadHeadToOSS(userId, file);
            }
            else if (type == "COS")
            {
                result = await _mediaService.UploadHeadToCOS(userId, file);
            }
            else if (type == "S3")
            {
                result = await _mediaService.UploadHeadToS3(userId, file);
            }
            else
            {
                throw new Exception("Unsupported media storage type");
            }

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

    }
}
