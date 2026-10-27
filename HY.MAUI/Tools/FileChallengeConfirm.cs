using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace HY.MAUI.Tools
{
    public static class FileChallengeConfirm
    {
        public static async Task<Dictionary<long, string>> ConfirmAsync(string filePath, long blockSize, IEnumerable<long> blockIndexes, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.RandomAccess);

            long fileSize = stream.Length;
            var result = new Dictionary<long, string>();
            byte[] buffer = new byte[81920];

            foreach (long index in blockIndexes.Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (index < 0) throw new ArgumentOutOfRangeException(nameof(blockIndexes));

                long totalBlocks = fileSize == 0 ? 0 : (fileSize - 1) / blockSize + 1;

                if (index >= totalBlocks) throw new ArgumentOutOfRangeException(nameof(blockIndexes));

                long offset = checked(index * blockSize);
                long remaining = Math.Min(blockSize, fileSize - offset);

                stream.Position = offset;

                using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

                while (remaining > 0)
                {
                    int count = (int)Math.Min(buffer.Length, remaining);

                    int read = await stream.ReadAsync(buffer.AsMemory(0, count), cancellationToken);

                    if (read == 0) throw new EndOfStreamException("文件读取不完整。");

                    md5.AppendData(buffer, 0, read);
                    remaining -= read;
                }

                result.Add(index, Convert.ToHexString(md5.GetHashAndReset()));
            }

            return result;
        }

    }
}
