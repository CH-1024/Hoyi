using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Interfaces
{
    public interface IGlobalCache
    {
        void SetCurrentUser(UserVM userVM);
        UserVM GetCurrentUser();
        void UpdateCurrentUser(UserVM currentUser);

        void SetLastEventId(long eventId);
        long GetLastEventId();
    }
}
