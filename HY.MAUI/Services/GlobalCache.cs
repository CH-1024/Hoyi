using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Services
{
    public class GlobalCache : IGlobalCache
    {
        private static readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();
        private static readonly string CurrentUserKey = "CurrentUser";
        private static readonly string LastEventIdKey = "LastEventId";

        private void SetCache(string key, object value)
        {
            _cache[key] = value;
        }

        private T? GetCache<T>(string key)
        {
            return _cache.TryGetValue(key, out var value) ? (T)value : default;
        }


        public void SetCurrentUser(UserVM userVM)
        {
            SetCache(CurrentUserKey, userVM);
        }

        public UserVM GetCurrentUser()
        {
            return GetCache<UserVM>(CurrentUserKey) ?? throw new InvalidOperationException("当前用户尚未初始化");
        }

        public void UpdateCurrentUser(UserVM currentUser)
        {
            var existingUser = GetCurrentUser();
            if (existingUser != null)
            {
                existingUser.Id = currentUser.Id;
                existingUser.HYid = currentUser.HYid;
                existingUser.Nickname = currentUser.Nickname;
                existingUser.Avatar = currentUser.Avatar;
                existingUser.Phone = currentUser.Phone;
                existingUser.Email = currentUser.Email;
                existingUser.Region = currentUser.Region;
                existingUser.Status = currentUser.Status;
                existingUser.Created_At = currentUser.Created_At;
            }
        }



        public void SetLastEventId(long eventId)
        {
            SetCache(LastEventIdKey, eventId);
        }

        public long GetLastEventId()
        {
            return GetCache<long?>(LastEventIdKey) ?? throw new InvalidOperationException("当前用户尚未初始化");
        }

    }
}
