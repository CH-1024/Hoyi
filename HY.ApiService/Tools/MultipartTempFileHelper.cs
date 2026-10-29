using HY.ApiService.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Buffers;
using System.Security.Cryptography;

namespace HY.ApiService.Tools
{
    public static class MultipartTempFileHelper
    {
        /// <summary>
        /// 从 multipart/form-data 请求中流式读取文件，
        /// 保存到系统临时文件，并计算 SHA256。
        /// </summary>
        public static async Task<UploadedFile> SaveToTempAsync(HttpRequest request, string fieldName = "file", long maxFileBytes = 1024L * 1024 * 1024, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (maxFileBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
            }

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
                BodyLengthLimit = maxFileBytes
            };

            string? tempFile = null;
            UploadedFile? result = null;
            bool completed = false;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);

            try
            {
                MultipartSection? section;

                while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
                {
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

                    // 仅保留文件名，不使用客户端提供的路径
                    fileName = Path.GetFileName(fileName.Replace('\\', '/'));

                    // 创建系统临时文件
                    tempFile = Path.GetTempFileName();

                    string contentType = section.ContentType ?? "application/octet-stream";

                    long length = 0;
                    string sha256;

                    using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    {
                        // 注意：临时文件已经存在，因此使用 FileMode.Create
                        await using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 128 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            int read;

                            while ((read = await section.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                // 检查文件大小，防止超出限制
                                if (length > maxFileBytes - read)
                                {
                                    throw new InvalidDataException("上传文件超过允许的大小限制");
                                }

                                var data = buffer.AsMemory(0, read);

                                // 写入临时文件
                                await output.WriteAsync(data, cancellationToken);

                                // 使用相同的数据块计算 SHA256
                                hash.AppendData(buffer.AsSpan(0, read));

                                length += read;
                            }

                            await output.FlushAsync(cancellationToken);
                        }

                        // 输出 64 位十六进制 SHA256
                        sha256 = Convert.ToHexString(hash.GetHashAndReset());
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
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
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
