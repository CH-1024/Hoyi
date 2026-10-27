using System.Security.Cryptography;

namespace HY.ApiService.Tools
{

    public sealed record FileChallenge
    {
        /// <summary>单个验证块大小，单位：字节</summary>
        public long BlockSize { get; init; }

        /// <summary>文件总块数</summary>
        public long TotalBlocks { get; init; }

        /// <summary>随机抽取的验证块序号集合，从 0 开始</summary>
        public required long[] BlockIndexes { get; init; }

        /// <summary>验证块序号与对应的 MD5（十六进制字符串）</summary>
        public required Dictionary<long, string> BlockMd5s { get; init; }
    }

    public static class FileChallengeGenerator
    {
        /// <summary>
        /// 生成文件随机抽样挑战。
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="blockSize">单个验证块大小，单位：字节</param>
        /// <param name="sampleCount">抽样块数量</param>
        public static async Task<FileChallenge> GenerateAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.RandomAccess);

            long fileSize = stream.Length;

            if (fileSize == 0) throw new ArgumentException("不能对空文件生成抽样挑战。");

            var (blockSize, sampleCount) = CalculateParameters(fileSize);

            if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize), "块大小必须大于 0。");
            if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount), "抽样数量必须大于 0。");


            // 向上取整，最后一个块允许不足 blockSize。
            long totalBlocks = (fileSize - 1) / blockSize + 1;

            if (sampleCount > totalBlocks) throw new ArgumentOutOfRangeException(nameof(sampleCount), $"抽样数量不能超过文件总块数 {totalBlocks}。");

            // Floyd 算法：不创建包含所有块序号的巨大数组。
            // 时间复杂度 O(sampleCount)，空间复杂度 O(sampleCount)。
            var selected = new HashSet<long>();

            for (long j = totalBlocks - sampleCount; j < totalBlocks; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long candidate = NextInt64(j + 1);

                if (!selected.Add(candidate)) selected.Add(j);
            }

            // 按序号排序，减少文件随机跳转时的磁盘寻道。
            long[] indexes = selected.OrderBy(x => x).ToArray();

            var md5s = new Dictionary<long, string>(indexes.Length);
            byte[] buffer = new byte[81920];

            foreach (long index in indexes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long offset = checked(index * blockSize);
                long length = Math.Min(blockSize, fileSize - offset);

                stream.Position = offset;

                using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

                long remaining = length;

                while (remaining > 0)
                {
                    int count = (int)Math.Min(buffer.Length, remaining);

                    int read = await stream.ReadAsync(buffer.AsMemory(0, count), cancellationToken);

                    if (read == 0) throw new EndOfStreamException("读取文件时遇到意外的文件结束。");

                    md5.AppendData(buffer, 0, read);
                    remaining -= read;
                }

                md5s.Add(index, Convert.ToHexString(md5.GetHashAndReset()));
            }

            return new FileChallenge
            {
                BlockSize = blockSize,
                TotalBlocks = totalBlocks,
                BlockIndexes = indexes,
                BlockMd5s = md5s
            };
        }

        /// <summary>
        /// 使用密码学安全随机数生成 [0, upperExclusive) 范围内的数。
        /// 支持 long 范围，避免取模偏差。
        /// </summary>
        private static long NextInt64(long upperExclusive)
        {
            if (upperExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(upperExclusive));

            ulong bound = (ulong)upperExclusive;

            // 拒绝采样，保证每个结果的概率相同。
            ulong threshold = unchecked(0UL - bound) % bound;
            Span<byte> bytes = stackalloc byte[8];

            while (true)
            {
                RandomNumberGenerator.Fill(bytes);

                ulong value = BitConverter.ToUInt64(bytes);

                if (value >= threshold) return (long)(value % bound);
            }
        }

        private static (long BlockSize, int SampleCount) CalculateParameters(long fileSize)
        {
            const long MinBlockSize = 256;               // 256 B
            const long MaxBlockSize = 1024 * 1024;       // 1 MiB
            const int TargetBlockCount = 4096;
            const int MinSampleCount = 4;
            const int MaxSampleCount = 128;

            // 根据文件大小动态调整块大小。
            long blockSize = Math.Clamp((fileSize - 1) / TargetBlockCount + 1, MinBlockSize, MaxBlockSize);

            long totalBlocks = (fileSize - 1) / blockSize + 1;

            // 默认至少抽取 4 块，小文件不足 4 块时全部抽取。
            // 大文件约抽取 1%，最多 128 块。
            long calculatedCount = Math.Max(MinSampleCount, (totalBlocks + 99) / 100);

            int sampleCount = (int)Math.Min(totalBlocks, Math.Min(MaxSampleCount, calculatedCount));

            return (blockSize, sampleCount);
        }

    }
}
