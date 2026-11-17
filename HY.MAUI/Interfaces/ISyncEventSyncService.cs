using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Interfaces
{
    public interface ISyncEventSyncService
    {
        Task<bool> InitCursorAsync();
        Task<bool> SyncAsync(CancellationToken cancellationToken = default);
    }
}
