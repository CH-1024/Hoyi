using Microsoft.Maui.Storage;

namespace HY.MAUI.Services
{
    public class SyncCursorStore
    {
        private static string Key(long userId) => $"hoyi_sync_cursor_{userId}";

        public bool TryGet(long userId, out long cursor)
        {
            var key = Key(userId);
            if (!Preferences.Default.ContainsKey(key))
            {
                cursor = 0;
                return false;
            }

            cursor = Preferences.Default.Get(key, 0L);
            return true;
        }

        public void Set(long userId, long cursor) =>
            Preferences.Default.Set(Key(userId), cursor);

        public void Clear(long userId) =>
            Preferences.Default.Remove(Key(userId));
    }
}
