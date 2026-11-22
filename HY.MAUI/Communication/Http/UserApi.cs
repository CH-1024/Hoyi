using HY.MAUI.Communication.Http.Requests;
using HY.MAUI.Models;
using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace HY.MAUI.Communication.Http
{
    public class UserApi : BaseApi
    {
        public UserApi(HttpClient http) : base(http)
        {

        }


        public async Task<Response?> GetInfo()
        {
            return await GetAsync(ApiUrl.GetInfo);
        }

        public async Task<Response?> UpdateHead(string url)
        {
            return await PostAsync($"{ApiUrl.UpdateHead}?url={url}");
        }

        public async Task<Response?> UpdateProfile(string nickname, string email, string phone)
        {
            var param = new UpdateUserRequest
            {
                Nickname = nickname,
                Email = email,
                Phone = phone
            };
            return await PostAsJsonAsync(ApiUrl.UpdateProfile, param);
        }
    }
}
