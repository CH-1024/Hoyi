using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace HY.MAUI.Communication.Http
{
    public class ContactApi : BaseApi
    {
        public ContactApi(HttpClient http) : base(http)
        {
        }


        public async Task<Response?> GetContactRequests()
        {
            return await GetAsync(ApiUrl.GetContactRequests);
        }

        public async Task<Response?> GetContacts()
        {
            return await GetAsync(ApiUrl.GetContacts);
        }

        public async Task<Response?> GetContact(long targetId)
        {
            return await GetAsync($"{ApiUrl.GetContact}?targetId={targetId}");
        }

        public async Task<Response?> SearchContact(string identity)
        {
            return await GetAsync($"{ApiUrl.SearchContact}?identity={identity}");
        }

        public async Task<Response?> RequestContact(long contactId, int source, string msg)
        {
            return await PostAsync($"{ApiUrl.RequestContact}?contactId={contactId}&source={source}&message={msg}");
        }

        public async Task<Response?> RespondContact(long contactRequestId, RespondContactHandle handle)
        {
            return await PostAsync($"{ApiUrl.RespondContact}?contactRequestId={contactRequestId}&handle={handle}");
        }

        public async Task<Response?> DeleteContact(long targetId)
        {
            return await DeleteAsync($"{ApiUrl.DeleteContact}?targetId={targetId}");
        }
    }

}
