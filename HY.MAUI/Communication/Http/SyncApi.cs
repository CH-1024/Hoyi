using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Communication.Http
{
    public class SyncApi : BaseApi
    {
        public SyncApi(HttpClient http) : base(http)
        {
        
        }


        public async Task<Response?> GetHead()
        {
            return await GetAsync(ApiUrl.SyncHead);
        }

        public async Task<Response?> GetChanges(long afterId, int take = 100)
        {
            return await GetAsync($"{ApiUrl.SyncChanges}?afterId={afterId}&take={take}");
        }

        public async Task<Response?> GetMessage(long chatId, long messageId)
        {
            return await GetAsync($"{ApiUrl.SyncMessage}?chatId={chatId}&messageId={messageId}");
        }
    }
}
