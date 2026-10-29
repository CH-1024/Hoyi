using HY.MAUI.Communication.Http.Requests;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Tools;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace HY.MAUI.Communication.Http
{
    public class FileApi : BaseApi
    {
        public FileApi(HttpClient http) : base(http)
        {

        }


        public async Task<Response?> UploadHead(FileResult? fileResult, CancellationToken token = default)
        {
            if (fileResult == null) return new Response(false, "文件不存在");

            await using var stream = await fileResult.OpenReadAsync();

            // 文件大小（字节）
            long size = stream.Length;

            if (size > 1 * 1024 * 1024) return new Response(false, "文件大小超过1 MiB的限制");

            // 计算 SHA256 Hash
            string fileSHA = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));

            var result = await PostAsync($"{ApiUrl.Challenge}?fileSHA256={fileSHA}&size={size}", token);
            if (result?.IsSucc != true) return result;

            var isExist = result.GetValue<bool>("IsExist");
            if (isExist)
            {
                var challengeId = result.GetValue<string>("ChallengeId");
                var blockSize = result.GetValue<long>("BlockSize");
                var blockIndexes = result.GetValue<long[]>("BlockIndexes");

                var blockMd5s = await FileChallengeConfirm.ConfirmAsync(fileResult.FullPath, blockSize, blockIndexes, token);

                var param = new ChallengeConfirmRequest
                {
                    ChallengeId = challengeId,
                    FileName = fileResult.FileName,
                    FileType = FileType.Image,
                    BlockMd5s = blockMd5s
                };

                return await PostAsJsonAsync(ApiUrl.ChallengeConfirm, param, token);
            }
            else
            {
                // 重置流的位置，以便后续读取
                stream.Position = 0;

                var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileResult.ContentType ?? "application/octet-stream");

                using var content = new MultipartFormDataContent();
                content.Add(streamContent, "file", fileResult.FileName);

                return await PostAsync(ApiUrl.UploadHead, content, token);
            }
        }

        public async Task<Response?> UploadImage(Guid taskId, FileResult? fileResult, CancellationToken token = default)
        {
            if (fileResult == null) return new Response(false, "文件不存在");

            await using var stream = await fileResult.OpenReadAsync();

            // 文件大小（字节）
            long size = stream.Length;

            if (size > 10 * 1024 * 1024) return new Response(false, "文件大小超过10 MiB的限制");

            // 计算 SHA256 Hash
            string fileSHA = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));

            var result = await PostAsync($"{ApiUrl.Challenge}?fileSHA256={fileSHA}&size={size}", token);
            if (result?.IsSucc != true) return result;

            var isExist = result.GetValue<bool>("IsExist");
            if (isExist)
            {
                var challengeId = result.GetValue<string>("ChallengeId");
                var blockSize = result.GetValue<long>("BlockSize");
                var blockIndexes = result.GetValue<long[]>("BlockIndexes");

                var blockMd5s = await FileChallengeConfirm.ConfirmAsync(fileResult.FullPath, blockSize, blockIndexes, token);

                var param = new ChallengeConfirmRequest
                {
                    ChallengeId = challengeId,
                    FileName = fileResult.FileName,
                    FileType = FileType.Image,
                    BlockMd5s = blockMd5s
                };

                return await PostAsJsonAsync(ApiUrl.ChallengeConfirm, param, token);
            }
            else
            {
                // 重置流的位置，以便后续读取
                stream.Position = 0;

                var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileResult.ContentType ?? "application/octet-stream");

                using var content = new MultipartFormDataContent();

                content.Headers.Add("File-Size", size.ToString());
                content.Headers.Add("File-UploadId", taskId.ToString());

                content.Add(streamContent, "file", fileResult.FileName);

                return await PostAsync(ApiUrl.UploadImage, content, token);
            }
        }

        public async Task<Response?> UploadVideo(Guid taskId, FileResult? fileResult, CancellationToken token = default)
        {
            if (fileResult == null) return new Response(false, "文件不存在");

            await using var stream = await fileResult.OpenReadAsync();

            // 文件大小（字节）
            long size = stream.Length;

            //if (size > 200 * 1024 * 1024) return new Response(false, "文件大小超过200 MiB的限制");

            // 计算 SHA256 Hash
            string fileSHA = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));

            var result = await PostAsync($"{ApiUrl.Challenge}?fileSHA256={fileSHA}&size={size}", token);
            if (result?.IsSucc != true) return result;

            var isExist = result.GetValue<bool>("IsExist");
            if (isExist)
            {
                var challengeId = result.GetValue<string>("ChallengeId");
                var blockSize = result.GetValue<long>("BlockSize");
                var blockIndexes = result.GetValue<long[]>("BlockIndexes");

                var blockMd5s = await FileChallengeConfirm.ConfirmAsync(fileResult.FullPath, blockSize, blockIndexes, token);

                var param = new ChallengeConfirmRequest
                {
                    ChallengeId = challengeId,
                    FileName = fileResult.FileName,
                    FileType = FileType.Video,
                    BlockMd5s = blockMd5s
                };

                return await PostAsJsonAsync(ApiUrl.ChallengeConfirm, param, token);
            }
            else
            {
                // 重置流的位置，以便后续读取
                stream.Position = 0;

                var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileResult.ContentType ?? "application/octet-stream");

                using var content = new MultipartFormDataContent();

                content.Headers.Add("File-Size", size.ToString());
                content.Headers.Add("File-UploadId", taskId.ToString());

                content.Add(streamContent, "file", fileResult.FileName);

                return await PostAsync(ApiUrl.UploadVideo, content, token);
            }
        }

    }
}
