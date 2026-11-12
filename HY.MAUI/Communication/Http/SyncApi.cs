using HY.MAUI.Dtos;

namespace HY.MAUI.Communication.Http
{
    public class SyncApi : BaseApi
    {
        public SyncApi(HttpClient http) : base(http) { }

        public Task<Response?> GetHead() => GetAsync(ApiUrl.SyncHead);

        public Task<Response?> GetChanges(long afterId, int take = 100) =>
            GetAsync($"{ApiUrl.SyncChanges}?afterId={afterId}&take={take}");

        public Task<Response?> GetMessage(long chatId, long messageId) =>
            GetAsync($"{ApiUrl.SyncMessage}?chatId={chatId}&messageId={messageId}");
    }
}
