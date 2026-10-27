namespace HY.ApiService.Extensions
{
    public static class DictionaryExtensions
    {
        public static bool DictionaryEquals<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue>? first, IReadOnlyDictionary<TKey, TValue>? second, IEqualityComparer<TValue>? valueComparer = null)
        {
            // 1. 引用相同（含都为 null）
            if (ReferenceEquals(first, second)) return true;

            // 2. 任一为 null
            if (first is null || second is null) return false;

            // 3. 数量不同直接返回
            if (first.Count != second.Count) return false;

            valueComparer ??= EqualityComparer<TValue>.Default;

            // 4. 用 second 的键比较器查找，逐个比较值
            foreach (var kv in first)
            {
                if (!second.TryGetValue(kv.Key, out var otherValue)) return false;

                if (!valueComparer.Equals(kv.Value, otherValue)) return false;
            }
            return true;
        }

    }
}
