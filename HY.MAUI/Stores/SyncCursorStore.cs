using HY.MAUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Stores
{
    public class SyncCursorStore
    {
        readonly IGlobalCache _globalCache;

        public SyncCursorStore(IGlobalCache globalCache)
        {
            _globalCache = globalCache;
        }

        public bool TryGet(out long cursor)
        {
            cursor = _globalCache.GetLastEventId();

            return true;
        }

        public void Set(long cursor)
        {
            _globalCache.SetLastEventId(cursor);
        }

    }
}
